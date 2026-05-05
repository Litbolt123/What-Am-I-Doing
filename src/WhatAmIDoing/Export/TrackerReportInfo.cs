namespace WhatAmIDoing.Export;

/// <summary>
/// Shown on the dashboard and in exported HTML so parents can tell installs apart and see that tracking ran.
/// </summary>
public sealed record TrackerReportInfo(
    string InstanceIdShort,
    string FullInstanceId,
    string FirstRunLocal,
    string ThisSessionStartLocal,
    string AppVersion,
    string DataFolderHint);
