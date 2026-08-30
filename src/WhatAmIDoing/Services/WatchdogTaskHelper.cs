using System.Diagnostics;

namespace WhatAmIDoing.Services;

/// <summary>
/// Optional Windows Task Scheduler registration so a small "wake" runs every few minutes
/// and starts the app if it is not already running. Not tamper-proof (a determined user can
/// disable the task) — optional family convenience, not kernel-level enforcement.
/// </summary>
public static class WatchdogTaskHelper
{
    public const string TaskName = "WhatAmIDoingFamilyRespawn";

    public static bool IsTaskRegistered()
    {
        try
        {
            using var p = CreateProcess(redirectOutput: true);
            p.StartInfo.ArgumentList.Add("/Query");
            p.StartInfo.ArgumentList.Add("/TN");
            p.StartInfo.ArgumentList.Add(TaskName);
            p.Start();
            p.WaitForExit(10_000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Creates a per-user scheduled task that runs every 5 minutes.</summary>
    public static bool TryRegister(string exePath)
    {
        try
        {
            using var p = CreateProcess();
            p.StartInfo.ArgumentList.Add("/Create");
            p.StartInfo.ArgumentList.Add("/F");
            p.StartInfo.ArgumentList.Add("/TN");
            p.StartInfo.ArgumentList.Add(TaskName);
            p.StartInfo.ArgumentList.Add("/TR");
            p.StartInfo.ArgumentList.Add($"\"{exePath}\" --spawn-if-stopped");
            p.StartInfo.ArgumentList.Add("/SC");
            p.StartInfo.ArgumentList.Add("MINUTE");
            p.StartInfo.ArgumentList.Add("/MO");
            p.StartInfo.ArgumentList.Add("5");
            p.StartInfo.ArgumentList.Add("/ST");
            p.StartInfo.ArgumentList.Add("00:00");
            p.StartInfo.ArgumentList.Add("/RL");
            p.StartInfo.ArgumentList.Add("LIMITED");
            p.StartInfo.ArgumentList.Add("/RU");
            p.StartInfo.ArgumentList.Add(Environment.UserName);
            p.Start();
            p.WaitForExit(60_000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryUnregister()
    {
        try
        {
            using var p = CreateProcess();
            p.StartInfo.ArgumentList.Add("/Delete");
            p.StartInfo.ArgumentList.Add("/F");
            p.StartInfo.ArgumentList.Add("/TN");
            p.StartInfo.ArgumentList.Add(TaskName);
            p.Start();
            p.WaitForExit(60_000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static Process CreateProcess(bool redirectOutput = false)
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput,
            },
        };
    }
}
