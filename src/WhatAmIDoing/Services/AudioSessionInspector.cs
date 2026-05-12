using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WhatAmIDoing.Services;

/// <summary>
/// Enumerates active Windows Core Audio (WASAPI) sessions and returns the set of process names
/// that currently have an audio render (speaker) or capture (microphone) session in the active state.
///
/// Microphone activity is the strongest "in a voice call" signal we can detect without speaking
/// to Discord/Teams/Zoom internals. We deliberately avoid claiming "Discord call" — the report
/// surfaces this as "voice / mic activity".
/// </summary>
public static class AudioSessionInspector
{
    /// <summary>
    /// Returns a comma-separated list of process names with at least one Active audio session,
    /// excluding the supplied <paramref name="excludeProcessName"/> (typically the foreground app
    /// — we only want companions). Returns null when nothing is active or on COM failure.
    /// </summary>
    public static string? Snapshot(string? excludeProcessName)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            CollectActiveSessionProcessNames(EDataFlow.eRender, names);
            CollectActiveSessionProcessNames(EDataFlow.eCapture, names);
        }
        catch
        {
            return null;
        }

        if (!string.IsNullOrEmpty(excludeProcessName))
            names.Remove(excludeProcessName);

        if (names.Count == 0)
            return null;

        return string.Join(",", names.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// True if the given process has at least one <see cref="AudioSessionState.Active"/> *render*
    /// (speaker/monitor) session. When the user is the foreground on that app and a documentary,
    /// browser video, or media player is producing sound, we can treat "no keyboard/mouse" as
    /// still <em>engaged</em> for idle purposes — the user is watching, not away.
    /// </summary>
    public static bool ForegroundAppHasActiveRenderAudio(string? processName) =>
        ForegroundAppRenderEngagement(processName, includePeakFallback: false);

    /// <summary>
    /// True when the foreground process has render audio we treat as "still engaged": either an
    /// <see cref="AudioSessionState.Active"/> session, or (when <paramref name="includePeakFallback"/>)
    /// a non-zero peak meter reading — helps when audio routes over HDMI/TV and Windows marks the
    /// session inactive intermittently.
    /// </summary>
    public static bool ForegroundAppRenderEngagement(string? processName, bool includePeakFallback)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? devices = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
                Type.GetTypeFromCLSID(MMDeviceEnumeratorClsid)!)!;

            if (enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE_ACTIVE, out devices) != 0
                || devices is null)
                return false;

            if (devices.GetCount(out var count) != 0)
                return false;

            for (var i = 0; i < count; i++)
            {
                IMMDevice? device = null;
                try
                {
                    if (devices.Item(i, out device) != 0 || device is null)
                        continue;

                    var iidSessionManager2 = IID_IAudioSessionManager2;
                    if (device.Activate(ref iidSessionManager2, CLSCTX_ALL, IntPtr.Zero, out var raw) != 0
                        || raw is null)
                        continue;

                    if (raw is not IAudioSessionManager2 mgr)
                    {
                        Marshal.ReleaseComObject(raw);
                        continue;
                    }

                    IAudioSessionEnumerator? sessions = null;
                    try
                    {
                        if (mgr.GetSessionEnumerator(out sessions) != 0 || sessions is null)
                            continue;

                        if (sessions.GetCount(out var sCount) != 0)
                            continue;

                        for (var j = 0; j < sCount; j++)
                        {
                            IAudioSessionControl? control = null;
                            try
                            {
                                if (sessions.GetSession(j, out control) != 0 || control is null)
                                    continue;

                                if (control is not IAudioSessionControl2 control2)
                                    continue;

                                if (control2.IsSystemSoundsSession() != 0)
                                    continue;

                                if (control2.GetProcessId(out var pid) != 0 || pid == 0)
                                    continue;

                                var name = SafeProcessName((int)pid);
                                if (string.IsNullOrEmpty(name)
                                    || !name.Equals(processName, StringComparison.OrdinalIgnoreCase))
                                    continue;

                                if (control.GetState(out var state) == 0 && state == AudioSessionState.Active)
                                    return true;

                                if (includePeakFallback
                                    && TryReadRenderPeak(control, out var peak)
                                    && peak > PeakEngagementEpsilon)
                                    return true;
                            }
                            catch
                            {
                                // skip this session
                            }
                            finally
                            {
                                if (control is not null)
                                    Marshal.ReleaseComObject(control);
                            }
                        }
                    }
                    finally
                    {
                        if (sessions is not null)
                            Marshal.ReleaseComObject(sessions);
                        Marshal.ReleaseComObject(mgr);
                    }
                }
                finally
                {
                    if (device is not null)
                        Marshal.ReleaseComObject(device);
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (devices is not null)
                Marshal.ReleaseComObject(devices);
            if (enumerator is not null)
                Marshal.ReleaseComObject(enumerator);
        }
    }

    private const float PeakEngagementEpsilon = 0.00015f;

    private static bool TryReadRenderPeak(IAudioSessionControl control, out float peak)
    {
        peak = 0;
        IntPtr unk;
        try
        {
            unk = Marshal.GetIUnknownForObject(control);
        }
        catch
        {
            return false;
        }

        try
        {
            var iid = IID_IAudioMeterInformation;
            if (Marshal.QueryInterface(unk, ref iid, out var pMeter) != 0 || pMeter == IntPtr.Zero)
                return false;
            try
            {
                var meter = (IAudioMeterInformation)Marshal.GetObjectForIUnknown(pMeter)!;
                return meter.GetPeakValue(out peak) == 0;
            }
            finally
            {
                Marshal.Release(pMeter);
            }
        }
        finally
        {
            Marshal.Release(unk);
        }
    }

    private static readonly Guid IID_IAudioMeterInformation = new("C02267F6-7CBA-44DA-8048-8D44CECD118C");

    [ComImport, Guid("C02267F6-7CBA-44DA-8048-8D44CECD118C"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float pfPeak);
    }

    private static void CollectActiveSessionProcessNames(EDataFlow flow, HashSet<string> outNames)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? devices = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
                Type.GetTypeFromCLSID(MMDeviceEnumeratorClsid)!)!;

            if (enumerator.EnumAudioEndpoints(flow, DEVICE_STATE_ACTIVE, out devices) != 0
                || devices is null)
                return;

            if (devices.GetCount(out var count) != 0)
                return;

            for (var i = 0; i < count; i++)
            {
                IMMDevice? device = null;
                try
                {
                    if (devices.Item(i, out device) != 0 || device is null)
                        continue;

                    var iidSessionManager2 = IID_IAudioSessionManager2;
                    if (device.Activate(ref iidSessionManager2, CLSCTX_ALL, IntPtr.Zero, out var raw) != 0
                        || raw is null)
                        continue;

                    if (raw is not IAudioSessionManager2 mgr)
                    {
                        Marshal.ReleaseComObject(raw);
                        continue;
                    }

                    IAudioSessionEnumerator? sessions = null;
                    try
                    {
                        if (mgr.GetSessionEnumerator(out sessions) != 0 || sessions is null)
                            continue;

                        if (sessions.GetCount(out var sCount) != 0)
                            continue;

                        for (var j = 0; j < sCount; j++)
                        {
                            IAudioSessionControl? control = null;
                            try
                            {
                                if (sessions.GetSession(j, out control) != 0 || control is null)
                                    continue;

                                if (control.GetState(out var state) != 0
                                    || state != AudioSessionState.Active)
                                    continue;

                                if (control is not IAudioSessionControl2 control2)
                                    continue;

                                if (control2.IsSystemSoundsSession() != 0)
                                    continue; // not S_OK

                                if (control2.GetProcessId(out var pid) != 0 || pid == 0)
                                    continue;

                                var name = SafeProcessName((int)pid);
                                if (!string.IsNullOrEmpty(name))
                                    outNames.Add(name);
                            }
                            catch
                            {
                                // skip this session
                            }
                            finally
                            {
                                if (control is not null)
                                    Marshal.ReleaseComObject(control);
                            }
                        }
                    }
                    finally
                    {
                        if (sessions is not null)
                            Marshal.ReleaseComObject(sessions);
                        Marshal.ReleaseComObject(mgr);
                    }
                }
                finally
                {
                    if (device is not null)
                        Marshal.ReleaseComObject(device);
                }
            }
        }
        finally
        {
            if (devices is not null)
                Marshal.ReleaseComObject(devices);
            if (enumerator is not null)
                Marshal.ReleaseComObject(enumerator);
        }
    }

    private static string? SafeProcessName(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    // --- COM interop -----------------------------------------------------------------------

    private static readonly Guid MMDeviceEnumeratorClsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IID_IAudioSessionManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private const uint CLSCTX_ALL = 0x1 | 0x2 | 0x4 | 0x10;
    private const uint DEVICE_STATE_ACTIVE = 0x00000001;

    private enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2,
    }

    private enum AudioSessionState
    {
        Inactive = 0,
        Active = 1,
        Expired = 2,
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int Item(int index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object? interfacePointer);
        [PreserveSig] int OpenPropertyStore(int access, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr sessionId, uint flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionId, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
        [PreserveSig] int RegisterSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr notification);
        [PreserveSig] int RegisterDuckNotification(IntPtr sessionId, IntPtr duckNotification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr duckNotification);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int sessionCount);
        [PreserveSig] int GetSession(int sessionCount, out IAudioSessionControl session);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out AudioSessionState state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid eventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid eventContext);
        [PreserveSig] int GetGroupingParam(out Guid grouping);
        [PreserveSig] int SetGroupingParam(ref Guid grouping, ref Guid eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notification);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        // Inherited IAudioSessionControl
        [PreserveSig] int GetState(out AudioSessionState state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid eventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid eventContext);
        [PreserveSig] int GetGroupingParam(out Guid grouping);
        [PreserveSig] int SetGroupingParam(ref Guid grouping, ref Guid eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notification);

        // IAudioSessionControl2 additions
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint processId);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference(int optOut);
    }
}
