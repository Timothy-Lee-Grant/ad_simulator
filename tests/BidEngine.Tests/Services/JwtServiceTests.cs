using BidEngine.Services;
using BidEngine.Shared;
using BidEngine.Shared.DTOs;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BidEngine.Tests.Services;

public class JwtServiceTests
{
    private readonly JwtService _jwtService;
    private readonly IConfiguration _configuration;

    public JwtServiceTests()
    {
        var settings = new Dictionary<string, string>
        {
            ["Jwt:Issuer"] = "BidEngine",
            ["Jwt:Audience"] = "BidEngineUsers",
            ["Jwt:SecretKey"] = "ThisIsASuperSecretKey1234567890!",
            ["Jwt:ExpirationMinutes"] = "60"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        _jwtService = new JwtService(_configuration);
    }

    [Fact]
    public void GenerateToken_ShouldReturnValidJwtToken()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User"
        };

        var token = _jwtService.GenerateToken(user, new[] { "Admin", "User" });

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(_jwtService.ValidateToken(token));

        var userId = _jwtService.GetUserIdFromToken(token);
        Assert.Equal(user.Id, userId);

        var roles = _jwtService.GetRolesFromToken(token).ToList();
        Assert.Contains("Admin", roles);
        Assert.Contains("User", roles);
    }

    [Fact]
    public void ValidateToken_ShouldReturnFalseForInvalidToken()
    {
        var invalidToken = "not-a-valid-token";
        Assert.False(_jwtService.ValidateToken(invalidToken));
    }
}
