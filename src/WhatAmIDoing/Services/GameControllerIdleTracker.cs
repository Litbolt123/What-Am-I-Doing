using System.Runtime.InteropServices;
using System.Threading;

namespace WhatAmIDoing.Services;

/// <summary>
/// Polls XInput gamepads so Bluetooth/Xbox-style controller input counts as user activity
/// alongside keyboard/mouse (<see cref="IdleHelper"/>). Wii Remote and some devices may not
/// appear here — pair with passive audio / per-process rule overrides when needed.
/// </summary>
internal static class GameControllerIdleTracker
{
    private const int ThumbDeadZone = 6000;
    private static readonly XinputGetStateDelegate? GetStateFn = ResolveGetState();
    private static readonly XINPUT_GAMEPAD[] _previous = new XINPUT_GAMEPAD[4];
    private static readonly bool[] _hasBaseline = new bool[4];
    private static long _lastActivityTick;

    /// <summary>Tick count (environment) of last detected controller input, or 0 if none yet.</summary>
    public static long LastActivityTick => Volatile.Read(ref _lastActivityTick);

    public static void Poll()
    {
        if (GetStateFn is null)
            return;

        var tick = Environment.TickCount64;
        for (var i = 0; i < 4; i++)
        {
            var st = new XINPUT_STATE();
            if (GetStateFn((uint)i, ref st) != 0)
            {
                _hasBaseline[i] = false;
                continue;
            }

            ref readonly var pad = ref st.Gamepad;
            if (!_hasBaseline[i])
            {
                _previous[i] = pad;
                _hasBaseline[i] = true;
                continue;
            }

            ref readonly var prev = ref _previous[i];
            if (pad.wButtons != prev.wButtons
                || pad.bLeftTrigger != prev.bLeftTrigger
                || pad.bRightTrigger != prev.bRightTrigger
                || OutsideDeadZone(pad.sThumbLX, pad.sThumbLY, prev.sThumbLX, prev.sThumbLY)
                || OutsideDeadZone(pad.sThumbRX, pad.sThumbRY, prev.sThumbRX, prev.sThumbRY))
            {
                Interlocked.Exchange(ref _lastActivityTick, tick);
                _previous[i] = pad;
            }
            else
            {
                _previous[i] = pad;
            }
        }
    }

    private static bool OutsideDeadZone(short ax, short ay, short bx, short by)
    {
        var dx = Math.Abs(ax - bx);
        var dy = Math.Abs(ay - by);
        return dx > ThumbDeadZone || dy > ThumbDeadZone;
    }

    private delegate uint XinputGetStateDelegate(uint dwUserIndex, ref XINPUT_STATE pState);

    private static XinputGetStateDelegate? ResolveGetState()
    {
        foreach (var dll in new[] { "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll" })
        {
            var h = NativeLibrary.Load(dll);
            try
            {
                var ptr = NativeLibrary.GetExport(h, "XInputGetState");
                return Marshal.GetDelegateForFunctionPointer<XinputGetStateDelegate>(ptr);
            }
            catch
            {
                try { NativeLibrary.Free(h); } catch { /* ignore */ }
            }
        }

        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }
}
