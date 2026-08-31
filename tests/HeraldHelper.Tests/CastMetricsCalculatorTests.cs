using HeraldHelper.Application.Services;
using HeraldHelper.Domain.Enums;
using HeraldHelper.Domain.Models;

namespace HeraldHelper.Tests;

public sealed class CastMetricsCalculatorTests
{
    [Fact]
    public void CalculateCastTime_FixedCastIgnoresStats()
    {
        var stats = new CharacterStatsSnapshot(
            ShardType.Eden,
            "Test",
            100, 100, 400, 100, 100, 100, 100, 100,
            25,
            10,
            DateTimeOffset.UtcNow);

        var result = CastMetricsCalculator.CalculateCastTime(3.0, stats, true, isFixedCastTime: true);

        Assert.Equal(3.0, result);
    }

    [Fact]
    public void CalculateCastTime_AdjustableCastClampsAtTwoSeconds()
    {
        var stats = new CharacterStatsSnapshot(
            ShardType.Eden,
            "Test",
            100, 100, 400, 100, 100, 100, 100, 100,
            25,
            10,
            DateTimeOffset.UtcNow);

        var result = CastMetricsCalculator.CalculateCastTime(2.5, stats, true, isFixedCastTime: false);

        Assert.Equal(2.0, result);
    }

    [Theory]
    [InlineData(1.5, 1.5)]
    [InlineData(1.0, 1.0)]
    [InlineData(0.5, 0.5)]
    public void CalculateCastTime_PreservesShorterBaseCasts(double baseSeconds, double expected)
    {
        var stats = new CharacterStatsSnapshot(
            ShardType.Eden,
            "Test",
            100, 100, 400, 100, 100, 100, 100, 100,
            25,
            10,
            DateTimeOffset.UtcNow);

        var result = CastMetricsCalculator.CalculateCastTime(baseSeconds, stats, true, isFixedCastTime: false);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateCastTime_DisabledDynamicCastReturnsBase()
    {
        var stats = new CharacterStatsSnapshot(
            ShardType.Eden,
            "Test",
            100, 100, 400, 100, 100, 100, 100, 100,
            25,
            10,
            DateTimeOffset.UtcNow);

        var result = CastMetricsCalculator.CalculateCastTime(2.5, stats, false, isFixedCastTime: false);

        Assert.Equal(2.5, result);
    }
}
