using System.Reflection;
using System.Text.Json;
using HookBridge.Application.DTOs.Auth;
using HookBridge.Application.Validation.Auth;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class SignupSecurityTests
{
    [Fact]
    public void RegisterRequestDto_DoesNotExposeTenantIdOrRole()
    {
        var properties = typeof(RegisterAdminRequestDto)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("TenantId", properties);
        Assert.DoesNotContain("Role", properties);
        Assert.Contains("Email", properties);
        Assert.Contains("Password", properties);
        Assert.Contains("OrganizationName", properties);
    }

    [Fact]
    public void RegisterValidator_HasNoRulesForTenantIdOrRole()
    {
        var validator = new RegisterAdminRequestDtoValidator();
        var descriptor = validator.CreateDescriptor();

        Assert.Empty(descriptor.GetRulesForMember("TenantId"));
        Assert.Empty(descriptor.GetRulesForMember("Role"));
        Assert.NotEmpty(descriptor.GetRulesForMember("Email"));
        Assert.NotEmpty(descriptor.GetRulesForMember("Password"));
    }

    [Fact]
    public void RegisterRequest_IgnoresClientSuppliedTenantIdAndRole_FromJson()
    {
        const string payload = """
            {
              "email": "user@example.com",
              "password": "Password123!",
              "organizationName": "Example Company",
              "tenantId": "attacker-tenant",
              "role": "Admin"
            }
            """;

        var dto = JsonSerializer.Deserialize<RegisterAdminRequestDto>(payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        Assert.NotNull(dto);
        Assert.Equal("user@example.com", dto!.Email);
        Assert.Equal("Password123!", dto.Password);
        Assert.Equal("Example Company", dto.OrganizationName);

        var properties = typeof(RegisterAdminRequestDto).GetProperties().Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("TenantId", properties);
        Assert.DoesNotContain("Role", properties);
    }

    [Fact]
    public void RegisterPage_DoesNotRenderTenantIdOrRoleInputs()
    {
        var registerPagePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/HookBridge.Dashboard/src/pages/RegisterPage.tsx"));
        var content = File.ReadAllText(registerPagePath);

        Assert.Contains("We’ll create your workspace automatically.", content);
        Assert.DoesNotContain("tenantId", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("role", content, StringComparison.OrdinalIgnoreCase);
    }
}
