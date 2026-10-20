using System.IO;
using System.Text.Json;
using WhatAmIDoing.Data;
using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

public static class RainmeterStatusService
{
    public const int WriteIntervalSeconds = 60;
    public const int ChangeThrottleSeconds = 5;
    public const int TodayCategoryLimit = 6;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string StatusPath =>
        Path.Combine(AppPaths.DataDirectory, "rainmeter-status.json");

    public static void Write(AppDatabase db, ActivityClassificationSnapshot.Result current)
    {
        try
        {
            var localToday = DateTime.Now.Date;
            var startUtc = localToday.ToUniversalTime();
            var endUtc = localToday.AddDays(1).ToUniversalTime();
            var counts = db.CountEngagedSamplesByCategoryBetween(startUtc, endUtc);
            var intervalSeconds = Math.Max(1, db.GetSampleIntervalMs() / 1000);

            long totalSamples = 0;
            var today = new List<RainmeterCategoryMinutes>(TodayCategoryLimit);
            foreach (var (category, sampleCount) in counts)
            {
                totalSamples += sampleCount;
                if (today.Count >= TodayCategoryLimit)
                    continue;
                var minutes = MinutesFromSamples(sampleCount, intervalSeconds);
                if (minutes <= 0)
                    continue;
                today.Add(new RainmeterCategoryMinutes
                {
                    Category = category,
                    Minutes = minutes,
                });
            }

            var status = new RainmeterStatus
            {
                UpdatedAt = DateTimeOffset.Now,
                Current = new RainmeterCurrent
                {
                    App = current.ProcessName,
                    Category = current.Category,
                    Title = current.WindowTitle ?? "",
                    State = current.State.ToDbString(),
                },
                Today = today,
                TodayTotalMinutes = MinutesFromSamples(totalSamples, intervalSeconds),
            };

            var json = JsonSerializer.Serialize(status, JsonOptions);
            var path = StatusPath;
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path))
                File.Replace(tmp, path, destinationBackupFileName: null);
            else
                File.Move(tmp, path);
        }
        catch
        {
            // A failed status write must not stop sampling.
        }
    }

    private static int MinutesFromSamples(long sampleCount, int intervalSeconds) =>
        (int)Math.Round(sampleCount * intervalSeconds / 60.0, MidpointRounding.AwayFromZero);

    private sealed class RainmeterStatus
    {
        public DateTimeOffset UpdatedAt { get; set; }
        public RainmeterCurrent Current { get; set; } = new();
        public List<RainmeterCategoryMinutes> Today { get; set; } = [];
        public int TodayTotalMinutes { get; set; }
    }

    private sealed class RainmeterCurrent
    {
        public string App { get; set; } = "";
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string State { get; set; } = "";
    }

    private sealed class RainmeterCategoryMinutes
    {
        public string Category { get; set; } = "";
        public int Minutes { get; set; }
    }
}
