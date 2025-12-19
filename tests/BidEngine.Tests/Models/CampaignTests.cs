using System;
using BidEngine.Shared;
using FluentAssertions;
using Xunit;

namespace BidEngine.Tests.Models;

public class CampaignTests
{
    [Fact]
    public void Campaign_CanServe_ReturnsTrue_WhenActiveAndWithinBudgets()
    {
        var c = new Campaign
        {
            Status = "active",
            DailyBudget = 100m,
            SpentToday = 50m,
            LifetimeBudget = 1000m,
            LifetimeSpent = 200m
        };

        c.CanServe.Should().BeTrue();
    }

    [Fact]
    public void Campaign_CanServe_ReturnsFalse_WhenNotActive()
    {
        var c = new Campaign { Status = "paused", DailyBudget = 100m, SpentToday = 0m };
        c.CanServe.Should().BeFalse();
    }

    [Fact]
    public void Campaign_CanServe_ReturnsFalse_WhenSpentEqualsDailyBudget()
    {
        var c = new Campaign { Status = "active", DailyBudget = 100m, SpentToday = 100m };
        c.CanServe.Should().BeFalse();
    }

    [Fact]
    public void Campaign_CanServe_ReturnsFalse_WhenLifetimeSpentEqualsLifetimeBudget()
    {
        var c = new Campaign
        {
            Status = "active",
            DailyBudget = 1000m,
            SpentToday = 0m,
            LifetimeBudget = 500m,
            LifetimeSpent = 500m
        };

        c.CanServe.Should().BeFalse();
    }

    [Fact]
    public void Campaign_CanServe_TreatsNullLifetimeBudgetAsUnlimited()
    {
        var c = new Campaign { Status = "active", DailyBudget = 100m, SpentToday = 0m, LifetimeBudget = null };
        c.CanServe.Should().BeTrue();
    }
}
