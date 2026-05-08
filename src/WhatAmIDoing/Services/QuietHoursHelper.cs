namespace WhatAmIDoing.Services;

/// <summary>
/// Local-hour quiet window (e.g. 21–7 spans midnight). End hour is exclusive when start &lt; end on same day;
/// when wrapping, hours in [start,24) or [0,end) are quiet.
/// </summary>
public static class QuietHoursHelper
{
    /// <summary>Returns true if <paramref name="hour"/> (0–23) falls in the quiet span.</summary>
    public static bool IsQuietHour(int hour, int quietStartHour, int quietEndHour)
    {
        quietStartHour = (quietStartHour % 24 + 24) % 24;
        quietEndHour = (quietEndHour % 24 + 24) % 24;
        if (quietStartHour == quietEndHour)
            return false;

        if (quietStartHour < quietEndHour)
            return hour >= quietStartHour && hour < quietEndHour;

        return hour >= quietStartHour || hour < quietEndHour;
    }
}
