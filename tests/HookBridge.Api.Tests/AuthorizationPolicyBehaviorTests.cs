using System.Security.Claims;
using System.Text.Encodings.Web;
using HookBridge.Api.Authorization;
using HookBridge.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class AuthorizationPolicyBehaviorTests
{
    [Fact]
    public async Task Owner_CanAccess_OwnerOnlyEndpoint()
    {
        using var server = BuildServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", AdminRole.Owner.ToString());

        var response = await client.GetAsync("/owner-only");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotAccess_OwnerOnlyEndpoint()
    {
        using var server = BuildServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", AdminRole.Admin.ToString());

        var response = await client.GetAsync("/owner-only");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Developer_CanAccess_DeveloperOrAboveEndpoint()
    {
        using var server = BuildServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", AdminRole.Developer.ToString());

        var response = await client.GetAsync("/developer-or-above");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CannotAccess_AdminOrOwnerEndpoint()
    {
        using var server = BuildServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", AdminRole.Viewer.ToString());

        var response = await client.GetAsync("/admin-or-owner");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Request_Returns401()
    {
        using var server = BuildServer();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/developer-or-above");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_ButUnauthorized_Request_Returns403()
    {
        using var server = BuildServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", AdminRole.Viewer.ToString());

        var response = await client.GetAsync("/owner-only");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static TestServer BuildServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(TestAuthHandler.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.OwnerOnly, policy =>
                policy.RequireRole(AdminRole.Owner.ToString()));

            options.AddPolicy(AuthorizationPolicies.AdminOrOwner, policy =>
                policy.RequireRole(AdminRole.Owner.ToString(), AdminRole.Admin.ToString()));

            options.AddPolicy(AuthorizationPolicies.DeveloperOrAbove, policy =>
                policy.RequireRole(AdminRole.Owner.ToString(), AdminRole.Admin.ToString(), AdminRole.Developer.ToString()));

            options.AddPolicy(AuthorizationPolicies.ViewerOrAbove, policy =>
                policy.RequireRole(AdminRole.Owner.ToString(), AdminRole.Admin.ToString(), AdminRole.Developer.ToString(), AdminRole.Viewer.ToString()));
        });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/owner-only", [Authorize(Policy = AuthorizationPolicies.OwnerOnly)] () => Results.Ok());
        app.MapGet("/admin-or-owner", [Authorize(Policy = AuthorizationPolicies.AdminOrOwner)] () => Results.Ok());
        app.MapGet("/developer-or-above", [Authorize(Policy = AuthorizationPolicies.DeveloperOrAbove)] () => Results.Ok());

        app.Start();
        return app.GetTestServer();
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Scheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var roleValue) || string.IsNullOrWhiteSpace(roleValue))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new("role", roleValue.ToString()),
            };

            var identity = new ClaimsIdentity(claims, Scheme, nameType: ClaimTypes.Name, roleType: "role");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
