using BidEngine.Services;
using BidEngine.Data;
using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using BidEngine.Shared.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BidEngine.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<RoleManager<ApplicationRole>> _roleManagerMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<AppDbContext> _dbContextMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var roleStoreMock = new Mock<IRoleStore<ApplicationRole>>();
        _roleManagerMock = new Mock<RoleManager<ApplicationRole>>(
            roleStoreMock.Object,
            null,
            null,
            null,
            null);

        _jwtServiceMock = new Mock<IJwtService>();
        _auditServiceMock = new Mock<IAuditService>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContextMock = new Mock<AppDbContext>(options);

        _authService = new AuthService(
            _userManagerMock.Object,
            _roleManagerMock.Object,
            _jwtServiceMock.Object,
            _auditServiceMock.Object,
            _dbContextMock.Object);
    }

    [Fact]
    public async Task LoginAsync_InvalidEmail_ShouldThrowUnauthorizedAccessException()
    {
        _userManagerMock.Setup(x => x.FindByEmailAsync("missing@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var request = new LoginRequestDto
        {
            Email = "missing@example.com",
            Password = "Password123!"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(request, "127.0.0.1", "unit-test"));
        _auditServiceMock.Verify(x => x.LogAsync(
            Guid.Empty,
            "LOGIN_FAILED",
            "User",
            null,
            null,
            null,
            "127.0.0.1",
            "unit-test",
            false,
            "Invalid credentials or inactive user"), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ShouldThrowUnauthorizedAccessException()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            UserName = "test@example.com",
            IsActive = true
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, "WrongPassword"))
            .ReturnsAsync(false);

        var request = new LoginRequestDto
        {
            Email = "test@example.com",
            Password = "WrongPassword"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.LoginAsync(request, "127.0.0.1", "unit-test"));
        _auditServiceMock.Verify(x => x.LogAsync(
            user.Id,
            "LOGIN_FAILED",
            "User",
            user.Id,
            null,
            null,
            "127.0.0.1",
            "unit-test",
            false,
            "Invalid password"), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ExistingEmail_ShouldThrowInvalidOperationException()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            UserName = "existing@example.com"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("existing@example.com"))
            .ReturnsAsync(user);

        var request = new RegisterRequestDto
        {
            Email = "existing@example.com",
            Password = "Password123!",
            FirstName = "Existing",
            LastName = "User"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _authService.RegisterAsync(request));
    }

    [Fact]
    public async Task ChangePasswordAsync_NonexistentUser_ShouldReturnFalse()
    {
        _userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        var request = new ChangePasswordDto
        {
            CurrentPassword = "Current1!",
            NewPassword = "NewPass1!"
        };

        var result = await _authService.ChangePasswordAsync(Guid.NewGuid(), request);
        Assert.False(result);
    }
}
