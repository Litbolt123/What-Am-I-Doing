using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using WhatAmIDoing.Export;
using WhatAmIDoing.Models;

namespace WhatAmIDoing.Data;

public sealed class AppDatabase
{
    private readonly string _connectionString;
    private readonly object _lock = new();

    public AppDatabase()
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = AppPaths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public void Initialize()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS settings (
                  key TEXT PRIMARY KEY,
                  value TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS rules (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  match_type INTEGER NOT NULL,
                  pattern TEXT NOT NULL,
                  category TEXT NOT NULL,
                  priority INTEGER NOT NULL DEFAULT 0,
                  ignore_in_totals INTEGER NOT NULL DEFAULT 0,
                  built_in INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS samples (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  ts_utc TEXT NOT NULL,
                  process_name TEXT NOT NULL,
                  window_title TEXT,
                  user_idle INTEGER NOT NULL,
                  category TEXT NOT NULL,
                  ignored INTEGER NOT NULL DEFAULT 0,
                  context_kind TEXT,
                  context_value TEXT,
                  companion_audio TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_samples_ts ON samples(ts_utc);
                CREATE TABLE IF NOT EXISTS screen_events (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  ts_utc TEXT NOT NULL,
                  image_path TEXT NOT NULL,
                  text TEXT,
                  foreground_process TEXT,
                  foreground_title TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_screen_events_ts ON screen_events(ts_utc);
                """;
            cmd.ExecuteNonQuery();

            EnsureRulesBuiltInColumn(conn);
            EnsureRulesIdleOverrideColumn(conn);
            EnsureRulesThinkingOverrideColumn(conn);
            EnsureRulesNotesColumn(conn);
            EnsureCategoryColorsTable(conn);
            EnsureSamplesContextColumns(conn);
            EnsureSamplesActivityStateColumn(conn);
            BackfillPresetBuiltInFlags(conn);
            MigrateCursorBuiltInThresholds(conn);
            MigrateSnappierIdleDefaults(conn);
            MigrateSampleAndYoutubeInstallerBaseline(conn);
            EnsureDefaultSettings(conn);
            EnsurePassiveVideoSettings(conn);
            SeedDefaultChartColorsIfEmpty(conn);
            SeedDefaultRulesIfEmpty(conn);
            TopUpMissingBuiltInRules(conn);
            EnsureTrackerIdentity(conn);
        }
    }

    private static bool ColumnExists(SqliteConnection conn, string table, string column)
    {
        using var pragma = conn.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({table})";
        using var reader = pragma.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void EnsureRulesBuiltInColumn(SqliteConnection conn)
    {
        if (ColumnExists(conn, "rules", "built_in"))
            return;

        using var alter = conn.CreateCommand();
        alter.CommandText = "ALTER TABLE rules ADD COLUMN built_in INTEGER NOT NULL DEFAULT 0";
        alter.ExecuteNonQuery();
    }

    private static void EnsureRulesIdleOverrideColumn(SqliteConnection conn)
    {
        if (ColumnExists(conn, "rules", "idle_threshold_ms_override"))
            return;

        using var alter = conn.CreateCommand();
        alter.CommandText = "ALTER TABLE rules ADD COLUMN idle_threshold_ms_override INTEGER";
        alter.ExecuteNonQuery();
    }

    private static void EnsureSamplesActivityStateColumn(SqliteConnection conn)
    {
        if (ColumnExists(conn, "samples", "activity_state"))
            return;

        using var alter = conn.CreateCommand();
        alter.CommandText = "ALTER TABLE samples ADD COLUMN activity_state TEXT";
        alter.ExecuteNonQuery();
    }

    private static void EnsureRulesThinkingOverrideColumn(SqliteConnection conn)
    {
        if (ColumnExists(conn, "rules", "thinking_extra_ms_override"))
            return;

        using var alter = conn.CreateCommand();
        alter.CommandText = "ALTER TABLE rules ADD COLUMN thinking_extra_ms_override INTEGER";
        alter.ExecuteNonQuery();
    }

    /// <summary>
    /// Free-form notes per rule — "why this rule exists", shared with parents. Added in
    /// step 14, optional on all existing rows (NULL on older DBs).
    /// </summary>
    private static void EnsureRulesNotesColumn(SqliteConnection conn)
    {
        if (ColumnExists(conn, "rules", "notes"))
            return;

        using var alter = conn.CreateCommand();
        alter.CommandText = "ALTER TABLE rules ADD COLUMN notes TEXT";
        alter.ExecuteNonQuery();
    }

    private static void EnsureCategoryColorsTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS category_colors (
              category TEXT PRIMARY KEY COLLATE NOCASE,
              color_hex TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>First-run chart/report colors (matches maintainer-tuned defaults). Skips if the user already has any row.</summary>
    private static void SeedDefaultChartColorsIfEmpty(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM category_colors";
        if (Convert.ToInt64(check.ExecuteScalar()) > 0)
            return;

        (string category, string hex)[] seed =
        [
            ("Activity tracker", "#00FF00"),
            ("Notes", "#EAD30D"),
            ("Windows (File Explorer)", "#FFFF80"),
            ("YouTube", "#FF8080"),
        ];
        foreach (var (category, hex) in seed)
        {
            using var ins = conn.CreateCommand();
            ins.CommandText = """
                INSERT OR IGNORE INTO category_colors(category, color_hex) VALUES ($c, $h);
                """;
            ins.Parameters.AddWithValue("$c", category);
            ins.Parameters.AddWithValue("$h", hex);
            ins.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Migrations for the Cursor built-in rule. We've landed on 30s idle + 30s thinking
    /// (Active 0–30s, Thinking 30–60s, Idle 60s+) after iterating twice. This upgrades any
    /// DB still on a prior Cursor default, but only when the user hasn't customized the row.
    /// </summary>
    private static void MigrateCursorBuiltInThresholds(SqliteConnection conn)
    {
        // From the earliest 5-min-only default.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE rules
                   SET idle_threshold_ms_override = 30000,
                       thinking_extra_ms_override = 30000
                 WHERE built_in = 1
                   AND match_type = 1
                   AND lower(trim(pattern)) = 'cursor'
                   AND idle_threshold_ms_override = 300000
                   AND thinking_extra_ms_override IS NULL;
                """;
            cmd.ExecuteNonQuery();
        }

        // From the intermediate 3 + 2 default.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE rules
                   SET idle_threshold_ms_override = 30000,
                       thinking_extra_ms_override = 30000
                 WHERE built_in = 1
                   AND match_type = 1
                   AND lower(trim(pattern)) = 'cursor'
                   AND idle_threshold_ms_override = 180000
                   AND thinking_extra_ms_override = 120000;
                """;
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// One-time cutover to the "snappier idle" defaults (halving Active → Idle time across the
    /// board). Only overwrites values that still match the older defaults, so users who have
    /// tuned Settings or a specific rule keep their preferences.
    /// </summary>
    private static void MigrateSnappierIdleDefaults(SqliteConnection conn)
    {
        // Global settings: 2 min → 1 min idle, 3 min → 1.5 min thinking.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE settings SET value = '60000'
                 WHERE key = 'idle_threshold_ms' AND value = '120000';
                """;
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE settings SET value = '90000'
                 WHERE key = 'thinking_extra_ms' AND value = '180000';
                """;
            cmd.ExecuteNonQuery();
        }

        // IDE / creative preset idle overrides: 5 min → 2.5 min. Exclude Cursor, which has
        // its own migration above. Only touches built-in rows whose value still equals the
        // old 5-minute preset.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE rules
                   SET idle_threshold_ms_override = 150000
                 WHERE built_in = 1
                   AND idle_threshold_ms_override = 300000
                   AND lower(trim(pattern)) <> 'cursor';
                """;
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Bumps sampling and YouTube scale only when the DB still has the older shipping defaults,
    /// so customized installs are untouched.
    /// </summary>
    private static void MigrateSampleAndYoutubeInstallerBaseline(SqliteConnection conn)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE settings SET value = '2000'
                 WHERE key = 'sample_interval_ms' AND value = '5000';
                """;
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE settings SET value = '10'
                 WHERE key = 'youtube_context_idle_scale'
                   AND (trim(value) = '4' OR trim(value) = '4.0');
                """;
            cmd.ExecuteNonQuery();
        }
    }

    private static void EnsureSamplesContextColumns(SqliteConnection conn)
    {
        if (!ColumnExists(conn, "samples", "context_kind"))
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE samples ADD COLUMN context_kind TEXT";
            alter.ExecuteNonQuery();
        }

        if (!ColumnExists(conn, "samples", "context_value"))
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE samples ADD COLUMN context_value TEXT";
            alter.ExecuteNonQuery();
        }

        if (!ColumnExists(conn, "samples", "companion_audio"))
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE samples ADD COLUMN companion_audio TEXT";
            alter.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Mark rows that exactly match the built-in list as presets (for DBs created before built_in existed).
    /// </summary>
    private static void BackfillPresetBuiltInFlags(SqliteConnection conn)
    {
        foreach (var br in BuiltInDefaultRules.All)
        {
            using var u = conn.CreateCommand();
            u.CommandText = """
                UPDATE rules SET built_in = 1
                WHERE match_type = $k
                  AND lower(trim(pattern)) = $pat
                  AND category = $cat
                  AND ignore_in_totals = $ign;
                """;
            u.Parameters.AddWithValue("$k", (int)br.MatchKind);
            u.Parameters.AddWithValue("$pat", br.Pattern.Trim().ToLowerInvariant());
            u.Parameters.AddWithValue("$cat", br.Category);
            u.Parameters.AddWithValue("$ign", br.IgnoreInTotals ? 1 : 0);
            u.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Replaces all rules with the built-in suggested set. Caller confirms in UI.
    /// </summary>
    public void RestoreBuiltInDefaultRules()
    {
        lock (_lock)
        {
            using var conn = Open();
            using (var clear = conn.CreateCommand())
            {
                clear.CommandText = "DELETE FROM rules";
                clear.ExecuteNonQuery();
            }

            foreach (var rule in BuiltInDefaultRules.All)
                InsertRule(conn, rule);
        }
    }

    private static void SeedDefaultRulesIfEmpty(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM rules";
        if (Convert.ToInt64(check.ExecuteScalar()) > 0)
            return;

        foreach (var rule in BuiltInDefaultRules.All)
            InsertRule(conn, rule);
    }

    /// <summary>
    /// Adds any built-in suggested rule whose (match_type, pattern) pair isn't already in the table.
    /// Runs on every launch so DBs seeded with older, smaller built-in lists pick up new suggestions
    /// (e.g. Comet browser, Sticky Notes, hardware monitoring tools) without clobbering anything the
    /// user has edited. Key property: if you've added a user rule with the same kind+pattern, we do
    /// <em>not</em> reinsert the built-in preset — your customization wins.
    /// </summary>
    private static void TopUpMissingBuiltInRules(SqliteConnection conn)
    {
        foreach (var rule in BuiltInDefaultRules.All)
        {
            using var check = conn.CreateCommand();
            check.CommandText = """
                SELECT COUNT(*) FROM rules
                WHERE match_type = $k
                  AND lower(trim(pattern)) = $p;
                """;
            check.Parameters.AddWithValue("$k", (int)rule.MatchKind);
            check.Parameters.AddWithValue("$p", rule.Pattern.Trim().ToLowerInvariant());
            if (Convert.ToInt64(check.ExecuteScalar()) > 0)
                continue;

            InsertRule(conn, rule);
        }
    }

    private static void InsertRule(SqliteConnection conn, BuiltInRule rule)
    {
        using var ins = conn.CreateCommand();
        ins.CommandText = """
            INSERT INTO rules(match_type, pattern, category, priority, ignore_in_totals, built_in,
                              idle_threshold_ms_override, thinking_extra_ms_override)
            VALUES ($k, $p, $c, $pr, $ign, 1, $iov, $tov);
            """;
        ins.Parameters.AddWithValue("$k", (int)rule.MatchKind);
        ins.Parameters.AddWithValue("$p", rule.Pattern);
        ins.Parameters.AddWithValue("$c", rule.Category);
        ins.Parameters.AddWithValue("$pr", rule.Priority);
        ins.Parameters.AddWithValue("$ign", rule.IgnoreInTotals ? 1 : 0);
        ins.Parameters.AddWithValue("$iov", (object?)rule.IdleThresholdMsOverride ?? DBNull.Value);
        ins.Parameters.AddWithValue("$tov", (object?)rule.ThinkingExtraMsOverride ?? DBNull.Value);
        ins.ExecuteNonQuery();
    }

    private static void DeleteBuiltInPresetsSamePattern(SqliteConnection conn, MatchKind kind, string pattern)
    {
        var key = pattern.Trim().ToLowerInvariant();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM rules
            WHERE built_in != 0
              AND match_type = $k
              AND lower(trim(pattern)) = $p
            """;
        cmd.Parameters.AddWithValue("$k", (int)kind);
        cmd.Parameters.AddWithValue("$p", key);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Site / YouTube / project / any-context share the same <c>pattern</c> column but
    /// different <c>match_type</c> values. Deleting a built-in row for one variant should
    /// also remove the others so "replace this preset" works when switching between
    /// page-only vs YouTube-only rules.
    /// </summary>
    private static void DeleteBuiltInContextRulesWithSamePattern(SqliteConnection conn, string pattern)
    {
        var key = pattern.Trim().ToLowerInvariant();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM rules
            WHERE built_in != 0
              AND match_type IN (3,4,5,6)
              AND lower(trim(pattern)) = $p
            """;
        cmd.Parameters.AddWithValue("$p", key);
        cmd.ExecuteNonQuery();
    }

    private static bool IsContextStyleMatchKind(MatchKind kind) =>
        kind is MatchKind.ContextValueContains
            or MatchKind.ContextSiteContains
            or MatchKind.ContextYouTubeVideoContains
            or MatchKind.ContextProjectContains;

    /// <summary>
    /// Adds a user rule. Any suggested (built-in) rule with the same match type and pattern is removed so your rule replaces only that preset.
    /// </summary>
    public void AddUserRule(MatchKind kind, string pattern, string category, int priority, bool ignoreInTotals,
        int? idleThresholdMsOverride = null,
        int? thinkingExtraMsOverride = null,
        string? notes = null)
    {
        lock (_lock)
        {
            using var conn = Open();
            if (IsContextStyleMatchKind(kind))
            {
                DeleteBuiltInContextRulesWithSamePattern(conn, pattern);
            }
            else
            {
                DeleteBuiltInPresetsSamePattern(conn, kind, pattern);
            }
            using var ins = conn.CreateCommand();
            ins.CommandText = """
                INSERT INTO rules(match_type, pattern, category, priority, ignore_in_totals, built_in,
                                  idle_threshold_ms_override, thinking_extra_ms_override, notes)
                VALUES ($k, $p, $c, $pr, $ign, 0, $iov, $tov, $n);
                """;
            ins.Parameters.AddWithValue("$k", (int)kind);
            ins.Parameters.AddWithValue("$p", pattern);
            ins.Parameters.AddWithValue("$c", category);
            ins.Parameters.AddWithValue("$pr", priority);
            ins.Parameters.AddWithValue("$ign", ignoreInTotals ? 1 : 0);
            ins.Parameters.AddWithValue("$iov", (object?)idleThresholdMsOverride ?? DBNull.Value);
            ins.Parameters.AddWithValue("$tov", (object?)thinkingExtraMsOverride ?? DBNull.Value);
            ins.Parameters.AddWithValue("$n",
                string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes.Trim());
            ins.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Updates an existing rule in place. The row becomes a user-owned rule (<c>built_in = 0</c>)
    /// even if it started as a suggested preset.
    /// </summary>
    public void UpdateUserRule(long id, MatchKind kind, string pattern, string category, int priority, bool ignoreInTotals,
        int? idleThresholdMsOverride = null,
        int? thinkingExtraMsOverride = null,
        string? notes = null)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var upd = conn.CreateCommand();
            upd.CommandText = """
                UPDATE rules SET
                  match_type = $k,
                  pattern = $p,
                  category = $c,
                  priority = $pr,
                  ignore_in_totals = $ign,
                  idle_threshold_ms_override = $iov,
                  thinking_extra_ms_override = $tov,
                  notes = $n,
                  built_in = 0
                WHERE id = $id;
                """;
            upd.Parameters.AddWithValue("$k", (int)kind);
            upd.Parameters.AddWithValue("$p", pattern);
            upd.Parameters.AddWithValue("$c", category);
            upd.Parameters.AddWithValue("$pr", priority);
            upd.Parameters.AddWithValue("$ign", ignoreInTotals ? 1 : 0);
            upd.Parameters.AddWithValue("$iov", (object?)idleThresholdMsOverride ?? DBNull.Value);
            upd.Parameters.AddWithValue("$tov", (object?)thinkingExtraMsOverride ?? DBNull.Value);
            upd.Parameters.AddWithValue("$n",
                string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes.Trim());
            upd.Parameters.AddWithValue("$id", id);
            upd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Copies the open database to another file using SQLite VACUUM INTO (safe copy while app is running).
    /// </summary>
    public void BackupDatabaseToFile(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            var pathForSql = destinationPath.Replace('\\', '/').Replace("'", "''", StringComparison.Ordinal);
            cmd.CommandText = $"VACUUM INTO '{pathForSql}'";
            cmd.ExecuteNonQuery();
        }
    }

    private static void EnsureTrackerIdentity(SqliteConnection conn)
    {
        using var readId = conn.CreateCommand();
        readId.CommandText = "SELECT value FROM settings WHERE key = 'install_instance_id'";
        var existingId = readId.ExecuteScalar() as string;
        if (string.IsNullOrEmpty(existingId))
        {
            existingId = Guid.NewGuid().ToString("N");
            UpsertSetting(conn, "install_instance_id", existingId);
            UpsertSetting(conn, "first_run_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        }

        var ver = typeof(AppDatabase).Assembly.GetName().Version?.ToString() ?? "1.0";
        UpsertSetting(conn, "app_version_last_run", ver);
        UpsertSetting(conn, "last_app_start_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
    }

    /// <summary>Stable per-install id (new guid when data file is new or replaced).</summary>
    public string GetTrackerInstallInstanceId() => GetSetting("install_instance_id") ?? "";

    public DateTime? GetTrackerFirstRunUtc()
    {
        var s = GetSetting("first_run_utc");
        if (string.IsNullOrEmpty(s))
            return null;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var u)
            ? u
            : null;
    }

    public DateTime? GetTrackerLastAppStartUtc()
    {
        var s = GetSetting("last_app_start_utc");
        if (string.IsNullOrEmpty(s))
            return null;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var u)
            ? u
            : null;
    }

    public TrackerReportInfo GetTrackerReportInfo()
    {
        var fullId = GetTrackerInstallInstanceId();
        var shortId = fullId.Length >= 8 ? fullId[..8] : fullId;
        var first = GetTrackerFirstRunUtc();
        var last = GetTrackerLastAppStartUtc();
        var ver = GetSetting("app_version_last_run") ?? "";
        var culture = CultureInfo.CurrentCulture;
        var firstLocal = first?.ToLocalTime().ToString("g", culture) ?? "—";
        var lastLocal = last?.ToLocalTime().ToString("g", culture) ?? "—";
        return new TrackerReportInfo(
            shortId,
            fullId,
            firstLocal,
            lastLocal,
            ver,
            AppPaths.DataDirectory);
    }

    /// <summary>Update just the per-rule idle threshold override for an existing rule.</summary>
    public void UpdateRuleIdleOverride(long id, int? idleThresholdMsOverride)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE rules SET idle_threshold_ms_override = $iov WHERE id = $id";
            cmd.Parameters.AddWithValue("$iov", (object?)idleThresholdMsOverride ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    private static void EnsureDefaultSettings(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM settings";
        var count = Convert.ToInt64(check.ExecuteScalar());
        if (count > 0)
            return;

        using var ins = conn.CreateCommand();
        ins.CommandText = """
            INSERT INTO settings(key, value) VALUES
              ('idle_threshold_ms', '60000'),
              ('thinking_extra_ms', '90000'),
              ('sample_interval_ms', '2000'),
              ('audio_detection_enabled', '1'),
              ('screens_enabled', '0'),
              ('screens_interval_ms', '60000'),
              ('screens_retention_days', '7'),
              ('screens_excluded_processes', 'KeePass,1Password,LastPass,Bitwarden,Dashlane,Enpass,Authy'),
              ('screens_paused_until_utc', '');
            """;
        ins.ExecuteNonQuery();
    }

    /// <summary>
    /// For existing databases: on by default, multiplies time before YouTube in-browser
    /// tabs look AFK (muted) and whether foreground *speaker* audio still counts as engaged.
    /// </summary>
    private static void EnsurePassiveVideoSettings(SqliteConnection conn)
    {
        void InsertIfMissing(string key, string def)
        {
            using var c = conn.CreateCommand();
            c.CommandText = "INSERT OR IGNORE INTO settings(key, value) VALUES($k, $v);";
            c.Parameters.AddWithValue("$k", key);
            c.Parameters.AddWithValue("$v", def);
            c.ExecuteNonQuery();
        }

        InsertIfMissing("passive_media_audio_engagement", "1");
        InsertIfMissing("youtube_context_idle_scale", "10");
    }

    /// <summary>Upper bound for <see cref="GetYouTubeContextIdleScale"/> (Settings UI and DB clamp).</summary>
    public const int YouTubeContextIdleScaleMax = 30;

    public int GetIdleThresholdMs()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = 'idle_threshold_ms'";
            var r = cmd.ExecuteScalar();
            return r is string s && int.TryParse(s, out var v) ? v : 60_000;
        }
    }

    public int GetSampleIntervalMs()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = 'sample_interval_ms'";
            var r = cmd.ExecuteScalar();
            return r is string s && int.TryParse(s, out var v) ? v : 2_000;
        }
    }

    public void SetIdleThresholdMs(int ms)
    {
        ms = Math.Clamp(ms, 15_000, 120 * 60_000);
        lock (_lock)
        {
            using var conn = Open();
            UpsertSetting(conn, "idle_threshold_ms", ms.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public void SetSampleIntervalMs(int ms)
    {
        ms = Math.Clamp(ms, 1_000, 120_000);
        lock (_lock)
        {
            using var conn = Open();
            UpsertSetting(conn, "sample_interval_ms", ms.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Extra grace (ms) beyond the idle threshold. Within this window a sample is
    /// classified as "Thinking" rather than full Idle. 0 disables the Thinking bucket.</summary>
    public int GetThinkingExtraMs()
    {
        var s = GetSetting("thinking_extra_ms");
        return int.TryParse(s, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? Math.Clamp(v, 0, 60 * 60_000)
            : 90_000;
    }

    public void SetThinkingExtraMs(int ms) =>
        SetSetting("thinking_extra_ms", Math.Clamp(ms, 0, 60 * 60_000)
            .ToString(System.Globalization.CultureInfo.InvariantCulture));

    public bool GetAudioDetectionEnabled()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = 'audio_detection_enabled'";
            var r = cmd.ExecuteScalar();
            if (r is string s && (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase)))
                return false;
            return true;
        }
    }

    public void SetAudioDetectionEnabled(bool enabled)
    {
        lock (_lock)
        {
            using var conn = Open();
            UpsertSetting(conn, "audio_detection_enabled", enabled ? "1" : "0");
        }
    }

    /// <summary>
    /// When on, if the <em>foreground</em> app has an active *speaker* (render) stream, we do
    /// not advance toward idle/Thinking from no input — you're treated as still engaged
    /// (documentary, YouTube, Netflix, music while that app is focused, etc.).
    /// </summary>
    public bool GetPassiveMediaAudioEngagementEnabled()
    {
        var s = GetSetting("passive_media_audio_engagement");
        if (s is "0" || s is { } t && (t.Equals("false", StringComparison.OrdinalIgnoreCase)
                                      || t.Equals("off", StringComparison.OrdinalIgnoreCase)))
            return false;
        return true;
    }

    public void SetPassiveMediaAudioEngagementEnabled(bool enabled) =>
        SetSetting("passive_media_audio_engagement", enabled ? "1" : "0");

    /// <summary>
    /// Multiplier applied to idle + Thinking time when the extractor classifies a tab as
    /// an in-browser <see cref="ContextKind.YouTube"/> video and the audio path above
    /// did <em>not</em> already keep you active (e.g. muted). Default 10× stretches muted YouTube
    /// runway before Thinking/Idle (clamped to <see cref="YouTubeContextIdleScaleMax"/> in Settings).
    /// </summary>
    public double GetYouTubeContextIdleScale()
    {
        var s = GetSetting("youtube_context_idle_scale");
        if (string.IsNullOrWhiteSpace(s) || !double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
            d = 10;
        return Math.Clamp(d, 1, YouTubeContextIdleScaleMax);
    }

    public void SetYouTubeContextIdleScale(double scale) =>
        SetSetting("youtube_context_idle_scale", Math.Clamp(scale, 1, YouTubeContextIdleScaleMax)
            .ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

    public string? GetSetting(string key)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = $k";
            cmd.Parameters.AddWithValue("$k", key);
            return cmd.ExecuteScalar() as string;
        }
    }

    public void SetSetting(string key, string value)
    {
        lock (_lock)
        {
            using var conn = Open();
            UpsertSetting(conn, key, value);
        }
    }

    public bool GetScreensEnabled()
    {
        var s = GetSetting("screens_enabled");
        return s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
    }

    public void SetScreensEnabled(bool enabled) => SetSetting("screens_enabled", enabled ? "1" : "0");

    public int GetScreensIntervalMs()
    {
        var s = GetSetting("screens_interval_ms");
        return int.TryParse(s, out var v) ? Math.Clamp(v, 5_000, 60 * 60_000) : 60_000;
    }

    public void SetScreensIntervalMs(int ms) =>
        SetSetting("screens_interval_ms", Math.Clamp(ms, 5_000, 60 * 60_000)
            .ToString(System.Globalization.CultureInfo.InvariantCulture));

    public int GetScreensRetentionDays()
    {
        var s = GetSetting("screens_retention_days");
        return int.TryParse(s, out var v) ? Math.Clamp(v, 1, 365) : 7;
    }

    public void SetScreensRetentionDays(int days) =>
        SetSetting("screens_retention_days", Math.Clamp(days, 1, 365)
            .ToString(System.Globalization.CultureInfo.InvariantCulture));

    public string GetScreensExcludedProcesses() =>
        GetSetting("screens_excluded_processes")
            ?? "KeePass,1Password,LastPass,Bitwarden,Dashlane,Enpass,Authy";

    public void SetScreensExcludedProcesses(string csv) => SetSetting("screens_excluded_processes", csv);

    public DateTime? GetScreensPausedUntilUtc()
    {
        var s = GetSetting("screens_paused_until_utc");
        if (string.IsNullOrEmpty(s))
            return null;
        return DateTime.TryParse(s, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt
            : null;
    }

    public void SetScreensPausedUntilUtc(DateTime? whenUtc) =>
        SetSetting("screens_paused_until_utc", whenUtc?.ToString("o") ?? "");

    /// <summary>Optional hex overrides (#RGB or #RRGGBB) for dashboard/HTML chart colors per category label.</summary>
    public bool TryGetCategoryColor(string category, out string colorHex)
    {
        colorHex = "";
        if (string.IsNullOrWhiteSpace(category))
            return false;
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT color_hex FROM category_colors
                WHERE lower(trim(category)) = lower(trim($c))
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$c", category);
            var o = cmd.ExecuteScalar();
            if (o is string s && s.Length > 0)
            {
                colorHex = s.Trim();
                return true;
            }
        }

        return false;
    }

    public void SetCategoryColor(string category, string colorHex)
    {
        if (string.IsNullOrWhiteSpace(category))
            return;
        var cat = category.Trim();
        var hex = colorHex.Trim();
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO category_colors(category, color_hex) VALUES ($c, $h)
                ON CONFLICT(category) DO UPDATE SET color_hex = excluded.color_hex;
                """;
            cmd.Parameters.AddWithValue("$c", cat);
            cmd.Parameters.AddWithValue("$h", hex);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteCategoryColor(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return;
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM category_colors WHERE lower(trim(category)) = lower(trim($c))";
            cmd.Parameters.AddWithValue("$c", category);
            cmd.ExecuteNonQuery();
        }
    }

    public List<(string Category, string ColorHex)> GetAllCategoryColors()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT category, color_hex FROM category_colors ORDER BY category COLLATE NOCASE";
            using var r = cmd.ExecuteReader();
            var list = new List<(string, string)>();
            while (r.Read())
                list.Add((r.GetString(0), r.GetString(1)));
            return list;
        }
    }

    public void InsertScreenEvent(DateTime tsUtc, string imagePath, string? text,
        string? foregroundProcess, string? foregroundTitle)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO screen_events(ts_utc, image_path, text, foreground_process, foreground_title)
                VALUES ($ts, $img, $text, $proc, $title);
                """;
            cmd.Parameters.AddWithValue("$ts", tsUtc.ToString("o"));
            cmd.Parameters.AddWithValue("$img", imagePath);
            cmd.Parameters.AddWithValue("$text", (object?)text ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$proc", (object?)foregroundProcess ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$title", (object?)foregroundTitle ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public List<ScreenEventRow> GetScreenEventsBetween(DateTime startUtc, DateTime endUtc)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, ts_utc, image_path, text, foreground_process, foreground_title
                FROM screen_events
                WHERE ts_utc >= $s AND ts_utc < $e
                ORDER BY ts_utc ASC;
                """;
            cmd.Parameters.AddWithValue("$s", startUtc.ToString("o"));
            cmd.Parameters.AddWithValue("$e", endUtc.ToString("o"));
            using var reader = cmd.ExecuteReader();
            var rows = new List<ScreenEventRow>();
            while (reader.Read())
            {
                rows.Add(new ScreenEventRow
                {
                    Id = reader.GetInt64(0),
                    TsUtc = DateTime.Parse(reader.GetString(1), null,
                        System.Globalization.DateTimeStyles.RoundtripKind),
                    ImagePath = reader.GetString(2),
                    Text = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ForegroundProcess = reader.IsDBNull(4) ? null : reader.GetString(4),
                    ForegroundTitle = reader.IsDBNull(5) ? null : reader.GetString(5),
                });
            }

            return rows;
        }
    }

    /// <summary>Deletes screen rows whose ts_utc is older than now-retention. Returns paths to delete on disk.</summary>
    public List<string> ExpireOldScreenEvents(int retentionDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays));
        var paths = new List<string>();
        lock (_lock)
        {
            using var conn = Open();
            using (var sel = conn.CreateCommand())
            {
                sel.CommandText = "SELECT image_path FROM screen_events WHERE ts_utc < $c";
                sel.Parameters.AddWithValue("$c", cutoff.ToString("o"));
                using var r = sel.ExecuteReader();
                while (r.Read())
                    paths.Add(r.GetString(0));
            }

            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM screen_events WHERE ts_utc < $c";
            del.Parameters.AddWithValue("$c", cutoff.ToString("o"));
            del.ExecuteNonQuery();
        }

        return paths;
    }

    private static void UpsertSetting(SqliteConnection conn, string key, string value)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings(key, value) VALUES ($k, $v)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<ClassificationRule> GetRules()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, match_type, pattern, category, priority, ignore_in_totals, built_in,
                       idle_threshold_ms_override, thinking_extra_ms_override, notes
                FROM rules
                ORDER BY priority DESC, id ASC;
                """;
            using var reader = cmd.ExecuteReader();
            var list = new List<ClassificationRule>();
            while (reader.Read())
            {
                list.Add(new ClassificationRule
                {
                    Id = reader.GetInt64(0),
                    MatchKind = (MatchKind)reader.GetInt32(1),
                    Pattern = reader.GetString(2),
                    Category = reader.GetString(3),
                    Priority = reader.GetInt32(4),
                    IgnoreInTotals = reader.GetInt32(5) != 0,
                    IsBuiltIn = reader.GetInt32(6) != 0,
                    IdleThresholdMsOverride = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    ThinkingExtraMsOverride = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    Notes = reader.IsDBNull(9) ? null : reader.GetString(9),
                });
            }

            return list;
        }
    }

    public void InsertSample(
        DateTime tsUtc,
        string processName,
        string? windowTitle,
        bool userIdle,
        string category,
        bool ignored,
        string? contextKind,
        string? contextValue,
        string? companionAudio,
        string activityState)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO samples(
                    ts_utc, process_name, window_title, user_idle, category, ignored,
                    context_kind, context_value, companion_audio, activity_state)
                VALUES ($ts, $proc, $title, $idle, $cat, $ign, $ck, $cv, $ca, $st);
                """;
            cmd.Parameters.AddWithValue("$ts", tsUtc.ToString("o"));
            cmd.Parameters.AddWithValue("$proc", processName);
            cmd.Parameters.AddWithValue("$title", windowTitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$idle", userIdle ? 1 : 0);
            cmd.Parameters.AddWithValue("$cat", category);
            cmd.Parameters.AddWithValue("$ign", ignored ? 1 : 0);
            cmd.Parameters.AddWithValue("$ck",
                string.IsNullOrEmpty(contextKind) ? (object)DBNull.Value : contextKind);
            cmd.Parameters.AddWithValue("$cv",
                string.IsNullOrEmpty(contextValue) ? (object)DBNull.Value : contextValue);
            cmd.Parameters.AddWithValue("$ca",
                string.IsNullOrEmpty(companionAudio) ? (object)DBNull.Value : companionAudio);
            cmd.Parameters.AddWithValue("$st", activityState);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteRule(long id)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM rules WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Lightweight count of samples in a window, split by whether each sample counted toward
    /// totals (non-ignored) vs was excluded by an "ignore in totals" rule. Multiplying by the
    /// sample interval gives a quick "time on computer" figure for dashboard cards without
    /// having to materialize every row.
    /// </summary>
    public (int Counted, int Ignored) CountSamplesBetween(DateTime startUtc, DateTime endUtc)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT
                    SUM(CASE WHEN ignored = 0 THEN 1 ELSE 0 END),
                    SUM(CASE WHEN ignored = 1 THEN 1 ELSE 0 END)
                FROM samples
                WHERE ts_utc >= $s AND ts_utc < $e;
                """;
            cmd.Parameters.AddWithValue("$s", startUtc.ToString("o"));
            cmd.Parameters.AddWithValue("$e", endUtc.ToString("o"));
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return (0, 0);
            var counted = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            var ignored = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            return (counted, ignored);
        }
    }

    public List<SampleRow> GetSamplesBetween(DateTime startUtc, DateTime endUtc)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT ts_utc, process_name, window_title, user_idle, category, ignored,
                       context_kind, context_value, companion_audio, activity_state
                FROM samples
                WHERE ts_utc >= $s AND ts_utc < $e
                ORDER BY ts_utc ASC;
                """;
            cmd.Parameters.AddWithValue("$s", startUtc.ToString("o"));
            cmd.Parameters.AddWithValue("$e", endUtc.ToString("o"));
            using var reader = cmd.ExecuteReader();
            var rows = new List<SampleRow>();
            while (reader.Read())
            {
                var userIdle = reader.GetInt32(3) != 0;
                rows.Add(new SampleRow
                {
                    TsUtc = DateTime.Parse(reader.GetString(0), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    ProcessName = reader.GetString(1),
                    WindowTitle = reader.IsDBNull(2) ? null : reader.GetString(2),
                    UserIdle = userIdle,
                    Category = reader.GetString(4),
                    Ignored = reader.GetInt32(5) != 0,
                    ContextKind = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ContextValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                    CompanionAudio = reader.IsDBNull(8) ? null : reader.GetString(8),
                    // Older samples (pre-Thinking bucket) have no state stored; derive one so the
                    // aggregator can treat them sensibly without a second pass.
                    ActivityState = reader.IsDBNull(9)
                        ? (userIdle ? "idle" : "active")
                        : reader.GetString(9),
                });
            }

            return rows;
        }
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}

public sealed class SampleRow
{
    public required DateTime TsUtc { get; init; }
    public required string ProcessName { get; init; }
    public string? WindowTitle { get; init; }
    public bool UserIdle { get; init; }
    public required string Category { get; init; }
    public bool Ignored { get; init; }

    /// <summary>"site", "youtube", "project" — see <see cref="WhatAmIDoing.Models.ContextKind"/>.</summary>
    public string? ContextKind { get; init; }

    /// <summary>The page title / video title / project folder extracted from the window title.</summary>
    public string? ContextValue { get; init; }

    /// <summary>Comma-separated process names with active audio render or capture sessions.</summary>
    public string? CompanionAudio { get; init; }

    /// <summary>"active" | "thinking" | "idle" — see <see cref="WhatAmIDoing.Models.ActivityState"/>.</summary>
    public string ActivityState { get; init; } = "active";
}

public sealed class ScreenEventRow
{
    public required long Id { get; init; }
    public required DateTime TsUtc { get; init; }
    public required string ImagePath { get; init; }
    public string? Text { get; init; }
    public string? ForegroundProcess { get; init; }
    public string? ForegroundTitle { get; init; }
}
