namespace WhatAmIDoing.Services;

public static class DashboardRefreshService
{
    public const string SettingContinuousRefresh = "dashboard_continuous_refresh";

    /// <summary>How often to reload the report while the dashboard is visible and continuous refresh is on.</summary>
    public static readonly TimeSpan ContinuousRefreshInterval = TimeSpan.FromSeconds(30);
}
