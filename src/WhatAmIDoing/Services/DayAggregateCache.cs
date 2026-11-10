using System.IO;
﻿using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using WhatAmIDoing.Data;
using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

public sealed class DayAggregatePayload
{
    public int TotalSamples { get; set; }
    public int SecondsIdle { get; set; }
    public int SecondsThinking { get; set; }
    public int SecondsIgnored { get; set; }
    public int VoiceWhileGamingSeconds { get; set; }
    public Dictionary<string, int> SecondsByCategory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ThinkingSecondsByCategory { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ActiveSecondsByProcess { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ThinkingSecondsByProcess { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ActiveSecondsBySite { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ActiveSecondsByYouTube { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ActiveSecondsByProject { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> CompanionAudioSeconds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Dictionary<string, int>> HourlySecondsByCategory { get; set; } = new();
    public int[] HourlyEngagedSeconds { get; set; } = new int[24];
    public Dictionary<string, int> DailyCategorySeconds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<DayDrillRow> DrillRows { get; set; } = new();
}

public sealed class DayDrillRow
{
    public string Category { get; set; } = "";
    public DateTime LocalTimestamp { get; set; }
    public string ProcessName { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public int Seconds { get; set; }
    public int ThinkingSeconds { get; set; }
}

public sealed class DayAggregateCache : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly string _connectionString;
    private readonly object _lock = new();
    private readonly Dictionary<string, (string RulesFp, int IntervalSec, long Count, string? MaxTs, DayAggregatePayload Payload)> _memory = new(StringComparer.Ordinal);

    public static DayAggregateCache Shared { get; } = new(AppPaths.DayCacheDatabasePath);

    public DayAggregateCache(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
        Initialize();
    }

    private void Initialize()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS day_aggregates (
                  day_local TEXT PRIMARY KEY,
                  rules_fp TEXT NOT NULL,
                  interval_sec INTEGER NOT NULL,
                  sample_count INTEGER NOT NULL,
                  max_ts_utc TEXT,
                  payload_json TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_day_aggregates_rules ON day_aggregates(rules_fp);
                """;
            cmd.ExecuteNonQuery();
        }
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public static string RulesFingerprint(IReadOnlyList<ClassificationRule> rules)
    {
        var sb = new StringBuilder(rules.Count * 48);
        foreach (var r in rules.OrderBy(x => x.Id))
        {
            sb.Append(r.Id).Append('|')
              .Append((int)r.MatchKind).Append('|')
              .Append(r.Pattern).Append('|')
              .Append(r.Category).Append('|')
              .Append(r.Priority).Append('|')
              .Append(r.IgnoreInTotals ? 1 : 0).Append('|')
              .Append(r.IdleThresholdMsOverride?.ToString() ?? "").Append('|')
              .Append(r.ThinkingExtraMsOverride?.ToString() ?? "").Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash.AsSpan(0, 16));
    }

    public static DayAggregatePayload FromReport(AggregatedReport report)
    {
        var hourly = new List<Dictionary<string, int>>(24);
        for (var h = 0; h < 24; h++)
        {
            hourly.Add(report.HourlySecondsByCategory[h]
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase));
        }

        var engaged = new int[24];
        if (report.DailyHourlyActiveSeconds.GetLength(0) > 0)
        {
            for (var h = 0; h < 24; h++)
                engaged[h] = report.DailyHourlyActiveSeconds[0, h];
        }

        var drills = new List<DayDrillRow>();
        foreach (var (cat, rows) in report.DrillDownByCategory)
        {
            foreach (var row in rows)
            {
                drills.Add(new DayDrillRow
                {
                    Category = cat,
                    LocalTimestamp = row.LocalTimestamp,
                    ProcessName = row.ProcessName,
                    WindowTitle = row.WindowTitle,
                    Seconds = row.Seconds,
                    ThinkingSeconds = row.ThinkingSeconds,
                });
            }
        }

        return new DayAggregatePayload
        {
            TotalSamples = report.TotalSamples,
            SecondsIdle = report.SecondsIdle,
            SecondsThinking = report.SecondsThinking,
            SecondsIgnored = report.SecondsIgnored,
            VoiceWhileGamingSeconds = report.VoiceWhileGamingSeconds,
            SecondsByCategory = report.SecondsByCategory.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            ThinkingSecondsByCategory = report.ThinkingSecondsByCategory.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            ActiveSecondsByProcess = report.ActiveSecondsByProcess.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            ThinkingSecondsByProcess = report.ThinkingSecondsByProcess.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            ActiveSecondsBySite = report.ActiveSecondsBySite.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            ActiveSecondsByYouTube = report.ActiveSecondsByYouTube.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            ActiveSecondsByProject = report.ActiveSecondsByProject.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            CompanionAudioSeconds = report.CompanionAudioSeconds.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            HourlySecondsByCategory = hourly,
            HourlyEngagedSeconds = engaged,
            DailyCategorySeconds = report.DailyCategorySeconds.Length > 0
                ? report.DailyCategorySeconds[0].ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            DrillRows = drills,
        };
    }

    public bool TryGet(
        DateOnly dayLocal,
        string rulesFp,
        int intervalSec,
        AppDatabase.SamplesFingerprint fingerprint,
        out DayAggregatePayload payload)
    {
        var key = dayLocal.ToString("yyyy-MM-dd");
        lock (_lock)
        {
            if (_memory.TryGetValue(key, out var mem)
                && mem.RulesFp == rulesFp
                && mem.IntervalSec == intervalSec
                && mem.Count == fingerprint.Count
                && mem.MaxTs == fingerprint.MaxTsUtc)
            {
                payload = mem.Payload;
                return true;
            }

            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT sample_count, max_ts_utc, payload_json
                FROM day_aggregates
                WHERE day_local = $d AND rules_fp = $r AND interval_sec = $i
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$d", key);
            cmd.Parameters.AddWithValue("$r", rulesFp);
            cmd.Parameters.AddWithValue("$i", intervalSec);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                payload = null!;
                return false;
            }

            var count = reader.GetInt64(0);
            var maxTs = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (count != fingerprint.Count || maxTs != fingerprint.MaxTsUtc)
            {
                payload = null!;
                return false;
            }

            var json = reader.GetString(2);
            payload = JsonSerializer.Deserialize<DayAggregatePayload>(json, JsonOpts)
                      ?? throw new InvalidOperationException("day cache payload decode failed");
            NormalizePayload(payload);
            _memory[key] = (rulesFp, intervalSec, count, maxTs, payload);
            return true;
        }
    }

    public void Upsert(
        DateOnly dayLocal,
        string rulesFp,
        int intervalSec,
        AppDatabase.SamplesFingerprint fingerprint,
        DayAggregatePayload payload)
    {
        var key = dayLocal.ToString("yyyy-MM-dd");
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO day_aggregates(day_local, rules_fp, interval_sec, sample_count, max_ts_utc, payload_json)
                VALUES ($d, $r, $i, $c, $m, $p)
                ON CONFLICT(day_local) DO UPDATE SET
                  rules_fp = excluded.rules_fp,
                  interval_sec = excluded.interval_sec,
                  sample_count = excluded.sample_count,
                  max_ts_utc = excluded.max_ts_utc,
                  payload_json = excluded.payload_json;
                """;
            cmd.Parameters.AddWithValue("$d", key);
            cmd.Parameters.AddWithValue("$r", rulesFp);
            cmd.Parameters.AddWithValue("$i", intervalSec);
            cmd.Parameters.AddWithValue("$c", fingerprint.Count);
            cmd.Parameters.AddWithValue("$m", (object?)fingerprint.MaxTsUtc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$p", json);
            cmd.ExecuteNonQuery();
            _memory[key] = (rulesFp, intervalSec, fingerprint.Count, fingerprint.MaxTsUtc, payload);
        }
    }

    private static void NormalizePayload(DayAggregatePayload payload)
    {
        payload.SecondsByCategory = new Dictionary<string, int>(payload.SecondsByCategory, StringComparer.OrdinalIgnoreCase);
        payload.ThinkingSecondsByCategory = new Dictionary<string, int>(payload.ThinkingSecondsByCategory, StringComparer.OrdinalIgnoreCase);
        payload.ActiveSecondsByProcess = new Dictionary<string, int>(payload.ActiveSecondsByProcess, StringComparer.OrdinalIgnoreCase);
        payload.ThinkingSecondsByProcess = new Dictionary<string, int>(payload.ThinkingSecondsByProcess, StringComparer.OrdinalIgnoreCase);
        payload.ActiveSecondsBySite = new Dictionary<string, int>(payload.ActiveSecondsBySite, StringComparer.OrdinalIgnoreCase);
        payload.ActiveSecondsByYouTube = new Dictionary<string, int>(payload.ActiveSecondsByYouTube, StringComparer.OrdinalIgnoreCase);
        payload.ActiveSecondsByProject = new Dictionary<string, int>(payload.ActiveSecondsByProject, StringComparer.OrdinalIgnoreCase);
        payload.CompanionAudioSeconds = new Dictionary<string, int>(payload.CompanionAudioSeconds, StringComparer.OrdinalIgnoreCase);
        if (payload.HourlyEngagedSeconds is null || payload.HourlyEngagedSeconds.Length != 24)
            payload.HourlyEngagedSeconds = new int[24];
        payload.DailyCategorySeconds ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        payload.DailyCategorySeconds = new Dictionary<string, int>(payload.DailyCategorySeconds, StringComparer.OrdinalIgnoreCase);
        while (payload.HourlySecondsByCategory.Count < 24)
            payload.HourlySecondsByCategory.Add(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        for (var h = 0; h < 24; h++)
            payload.HourlySecondsByCategory[h] = new Dictionary<string, int>(payload.HourlySecondsByCategory[h], StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose() { }
}
