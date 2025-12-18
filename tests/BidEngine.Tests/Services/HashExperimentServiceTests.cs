using System;
using BidEngine.Services;
using FluentAssertions;
using Xunit;

namespace BidEngine.Tests.Services;

public class HashExperimentServiceTests
{
    [Fact]
    public void GetVariant_IsDeterministic_ForSameIdentity()
    {
        var svc = new HashExperimentService();
        var v1 = svc.GetVariant("bid-selector", "user123");
        var v2 = svc.GetVariant("bid-selector", "user123");
        v1.Should().Be(v2);
    }

    [Fact]
    public void GetVariant_ReturnsKnownVariants()
    {
        var svc = new HashExperimentService();
        var v = svc.GetVariant("bid-selector", "user456");
        v.Should().MatchRegex("A|B|control");
    }
}
