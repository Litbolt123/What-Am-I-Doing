using System.Runtime.InteropServices;

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

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }
}
