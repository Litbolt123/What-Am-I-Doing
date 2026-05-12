using System.IO;
using System.Linq;
using System.Text.Json;

namespace WhatAmIDoing.Services;

/// <summary>Persists last calibration suggestions so the tune-up dialog can show prior hints.</summary>
public static class TuningHintsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WhatAmIDoing",
        "tuning_hints.json");

    public static List<TuningHintRecord> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new List<TuningHintRecord>();
            var json = File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<List<TuningHintRecord>>(json, JsonOpts);
            return list ?? new List<TuningHintRecord>();
        }
        catch
        {
            return new List<TuningHintRecord>();
        }
    }

    public static void Upsert(TuningHintRecord hint)
    {
        var list = Load();
        list.RemoveAll(h => h.ProcessName.Equals(hint.ProcessName, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, hint);
        while (list.Count > 40)
            list.RemoveAt(list.Count - 1);

        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list, JsonOpts));
        }
        catch
        {
            /* best-effort */
        }
    }

    public static TuningHintRecord? LastForProcess(string processName)
    {
        return Load().FirstOrDefault(h =>
            h.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class TuningHintRecord
{
    public string ProcessName { get; set; } = "";
    public string Intent { get; set; } = "";
    public int SuggestedIdleMs { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
