using BidEngine.Data;
using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using BidEngine.Shared.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BidEngine.Services;

/// <summary>
/// Authentication service implementation
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IJwtService _jwtService;
    private readonly IAuditService _auditService;
    private readonly AppDbContext _context;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IJwtService jwtService,
        IAuditService auditService,
        AppDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
        _auditService = auditService;
        _context = context;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, string ipAddress, string userAgent)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
        {
            await _auditService.LogAsync(
                Guid.Empty,
                "LOGIN_FAILED",
                "User",
                null,
                null,
                null,
                ipAddress,
                userAgent,
                false,
                "Invalid credentials or inactive user"
            );
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        var isValidPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValidPassword)
        {
            await _auditService.LogAsync(
                user.Id,
                "LOGIN_FAILED",
                "User",
                user.Id,
                null,
                null,
                ipAddress,
                userAgent,
                false,
                "Invalid password"
            );
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtService.GenerateToken(user, roles);

        await _auditService.LogAsync(
            user.Id,
            "LOGIN_SUCCESS",
            "User",
            user.Id,
            null,
            null,
            ipAddress,
            userAgent,
            true
        );

        return new LoginResponseDto
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60), // Should come from config
            User = await GetUserDtoAsync(user)
        };
    }

    public async Task<UserDto> RegisterAsync(RegisterRequestDto request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = true // For simplicity in this demo
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        // Assign default role
        await _userManager.AddToRoleAsync(user, "User");

        await _auditService.LogAsync(
            user.Id,
            "USER_REGISTERED",
            "User",
            user.Id,
            null,
            System.Text.Json.JsonSerializer.Serialize(new { request.Email, request.FirstName, request.LastName }),
            null,
            null,
            true
        );

        return await GetUserDtoAsync(user);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            await _auditService.LogAsync(
                userId,
                "CHANGE_PASSWORD_FAILED",
                "User",
                userId,
                null,
                null,
                null,
                null,
                false,
                string.Join(", ", result.Errors.Select(e => e.Description))
            );
            return false;
        }

        await _auditService.LogAsync(
            userId,
            "CHANGE_PASSWORD_SUCCESS",
            "User",
            userId,
            null,
            null,
            null,
            null,
            true
        );

        return true;
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        return Task.FromResult(_jwtService.ValidateToken(token));
    }

    public async Task<UserDto?> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user != null ? await GetUserDtoAsync(user) : null;
    }

    public async Task LogoutAsync(Guid userId)
    {
        await _auditService.LogAsync(
            userId,
            "LOGOUT",
            "User",
            userId,
            null,
            null,
            null,
            null,
            true
        );
    }

    private async Task<UserDto> GetUserDtoAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            Roles = roles.ToList()
        };
    }
}