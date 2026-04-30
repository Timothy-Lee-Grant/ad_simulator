using BidEngine.Shared;
using BidEngine.Shared.DTOs;

namespace BidEngine.Services.Interfaces;

/// <summary>
/// Interface for authentication services
/// </summary>
public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, string ipAddress, string userAgent);
    Task<UserDto> RegisterAsync(RegisterRequestDto request);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto request);
    Task<bool> ValidateTokenAsync(string token);
    Task<UserDto?> GetCurrentUserAsync(Guid userId);
    Task LogoutAsync(Guid userId);
}

/// <summary>
/// Interface for JWT token services
/// </summary>
public interface IJwtService
{
    string GenerateToken(ApplicationUser user, IEnumerable<string> roles);
    bool ValidateToken(string token);
    Guid? GetUserIdFromToken(string token);
    IEnumerable<string> GetRolesFromToken(string token);
}

/// <summary>
/// Interface for audit logging services
/// </summary>
public interface IAuditService
{
    Task LogAsync(Guid userId, string action, string entityType, Guid? entityId = null,
                  string? oldValues = null, string? newValues = null,
                  string? ipAddress = null, string? userAgent = null,
                  bool success = true, string? errorMessage = null);
    Task<IEnumerable<AuditLog>> GetAuditLogsAsync(int page = 1, int pageSize = 50);
    Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(Guid userId, int page = 1, int pageSize = 50);
}