using WhatAmIDoing.Services;
using Xunit;

namespace WhatAmIDoing.Tests;

public class ChartLayoutTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(28)]
    public void DailyChartContentHeight_MatchesLastRowBottom(int dayCount)
    {
        var h = ChartRenderer.DailyChartContentHeight(dayCount);
        var lastBottom = (dayCount - 1) * (ChartRenderer.DailyRowHeight + ChartRenderer.DailyRowSpacing)
                         + ChartRenderer.DailyRowHeight;
        Assert.Equal(h, lastBottom);
        Assert.Equal(0, h % 1); // integer px with 26/6 metrics
    }
}
