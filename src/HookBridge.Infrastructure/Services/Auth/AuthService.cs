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
using HookBridge.Domain.Configuration;
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

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingAdmin = await adminUserRepository.FirstOrDefaultAsync(
            x => x.Email == normalizedEmail,
            cancellationToken);

        if (existingAdmin is not null)
        {
            throw new ConflictException("An admin user with this email already exists.");
        }

        var now = dateTimeProvider.UtcNow;
        var organizationName = string.IsNullOrWhiteSpace(request.OrganizationName)
            ? DeriveOrganizationNameFromEmail(normalizedEmail)
            : request.OrganizationName.Trim();

        var tenant = new Tenant
        {
            Id = guidGenerator.NewGuid(),
            Name = organizationName,
            Slug = BuildSlug(organizationName),
            Status = TenantStatus.Active,
            Plan = BillingPlan.Free,
            MonthlyEventLimit = BillingPlanLimits.Free,
            CreatedAt = now,
        };

        await tenantRepository.AddAsync(tenant, cancellationToken);

        var adminUser = new AdminUser
        {
            Id = guidGenerator.NewGuid(),
            TenantId = tenant.Id,
            Email = normalizedEmail,
            PasswordHash = passwordHasher.HashPassword(request.Password),
            FullName = organizationName,
            Role = AdminRole.Owner,
            IsActive = true,
            LastLoginAt = now,
            CreatedAt = now,
        };

        await adminUserRepository.AddAsync(adminUser, cancellationToken);

        return BuildAuthResponse(adminUser, tenant.Name);
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

    private AuthResponseDto BuildAuthResponse(AdminUser adminUser, string? organizationName = null)
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
                OrganizationName = organizationName ?? string.Empty,
            },
        };
    }

    private static string DeriveOrganizationNameFromEmail(string email)
    {
        var atIndex = email.IndexOf("@", StringComparison.Ordinal);
        if (atIndex < 0 || atIndex == email.Length - 1) return "My Organization";
        var domain = email[(atIndex + 1)..];
        var root = domain.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(root) ? "My Organization" : char.ToUpperInvariant(root[0]) + root[1..];
    }

    private static string BuildSlug(string name)
    {
        var slug = new string(name.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        slug = string.Join("-", slug.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? "my-organization" : slug;
    }
}
