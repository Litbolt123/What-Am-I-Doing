using System.Runtime.InteropServices;
using System.Threading;

namespace WhatAmIDoing.Services;

public static class IdleHelper
{
    public static TimeSpan GetIdleTime()
    {
        var li = new LastInputInfo { CbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref li))
            return TimeSpan.Zero;

        var tick = Environment.TickCount64;
        var idleMs = tick - li.DwTime;
        if (idleMs < 0)
            idleMs = 0;
        return TimeSpan.FromMilliseconds(idleMs);
    }

    /// <summary>
    /// Minimum of keyboard/mouse idle and gamepad idle when <paramref name="controllerEngagementEnabled"/>.
    /// Gamepads are polled via <see cref="GameControllerIdleTracker"/> (XInput).
    /// </summary>
    public static TimeSpan GetCombinedIdleTime(bool controllerEngagementEnabled)
    {
        var kbMs = GetIdleTime().TotalMilliseconds;
        if (!controllerEngagementEnabled)
            return TimeSpan.FromMilliseconds(kbMs);

        var padTick = GameControllerIdleTracker.LastActivityTick;
        if (padTick == 0)
            return TimeSpan.FromMilliseconds(kbMs);

        var padIdleMs = Environment.TickCount64 - padTick;
        if (padIdleMs < 0)
            padIdleMs = 0;

        var combined = Math.Min(kbMs, padIdleMs);
        return TimeSpan.FromMilliseconds(combined);
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }
}
