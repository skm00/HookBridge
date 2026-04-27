using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using HookBridge.Application.DTOs.Auth;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Security;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HookBridge.Infrastructure.Services.Auth;

public sealed class AuthService(
    IMongoRepository<AdminUser> adminUserRepository,
    IMongoRepository<Tenant> tenantRepository,
    IGuidGenerator guidGenerator,
    IDateTimeProvider dateTimeProvider,
    IPasswordHasher passwordHasher,
    IValidator<RegisterAdminRequestDto> registerValidator,
    IValidator<LoginRequestDto> loginValidator,
    IOptions<JwtSettings> jwtSettingsOptions) : IAuthService
{
    private const string InvalidLoginMessage = "Invalid email or password.";

    private readonly JwtSettings _jwtSettings = jwtSettingsOptions.Value;

    public async Task<AuthResponseDto> RegisterAsync(RegisterAdminRequestDto request, CancellationToken cancellationToken = default)
    {
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tenant = await tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant is null || tenant.Status != TenantStatus.Active)
        {
            throw new KeyNotFoundException($"Active tenant '{request.TenantId}' not found.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingAdmin = await adminUserRepository.FirstOrDefaultAsync(
            x => x.TenantId == request.TenantId && x.Email == normalizedEmail,
            cancellationToken);

        if (existingAdmin is not null)
        {
            throw new ConflictException("An admin user with this email already exists for the tenant.");
        }

        var now = dateTimeProvider.UtcNow;
        var adminUser = new AdminUser
        {
            Id = guidGenerator.NewGuid(),
            TenantId = request.TenantId,
            Email = normalizedEmail,
            PasswordHash = passwordHasher.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Role = request.Role ?? AdminRole.Viewer,
            IsActive = true,
            LastLoginAt = now,
            CreatedAt = now,
        };

        await adminUserRepository.AddAsync(adminUser, cancellationToken);

        return BuildAuthResponse(adminUser);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var adminUser = await adminUserRepository.FirstOrDefaultAsync(x => x.Email == normalizedEmail && x.IsActive, cancellationToken);

        if (adminUser is null || !passwordHasher.VerifyPassword(request.Password, adminUser.PasswordHash))
        {
            throw new UnauthorizedException(InvalidLoginMessage);
        }

        var now = dateTimeProvider.UtcNow;
        adminUser.LastLoginAt = now;
        adminUser.UpdatedAt = now;

        await adminUserRepository.UpdateAsync(adminUser, cancellationToken);

        return BuildAuthResponse(adminUser);
    }

    private AuthResponseDto BuildAuthResponse(AdminUser adminUser)
    {
        var expiresAt = dateTimeProvider.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, adminUser.Id),
            new("tenantId", adminUser.TenantId),
            new(JwtRegisteredClaimNames.Email, adminUser.Email),
            new("fullName", adminUser.FullName),
            new("role", adminUser.Role.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthResponseDto
        {
            Token = tokenValue,
            ExpiresAtUtc = expiresAt,
            User = new AdminUserResponseDto
            {
                Id = adminUser.Id,
                TenantId = adminUser.TenantId,
                Email = adminUser.Email,
                FullName = adminUser.FullName,
                IsActive = adminUser.IsActive,
                LastLoginAt = adminUser.LastLoginAt,
                Role = adminUser.Role,
            },
        };
    }
}
