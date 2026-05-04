using System.Net;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class DemoSeedEndpointTests
{
    [Fact]
    public async Task DevSeedEndpoint_IsUnavailableInProduction()
    {
        using var host = await BuildHostAsync(Environments.Production);
        var client = host.CreateClient();

        var response = await client.PostAsync("/api/v1/dev/demo/seed", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<TestServer> BuildHostAsync(string environmentName)
    {
        var builder = new WebHostBuilder()
            .UseEnvironment(environmentName)
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton<IDemoDataSeeder, NoOpDemoDataSeeder>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    if (app.ApplicationServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
                    {
                        endpoints.MapPost("/api/v1/dev/demo/seed", async context =>
                        {
                            var seeder = context.RequestServices.GetRequiredService<IDemoDataSeeder>();
                            await seeder.SeedAsync(context.RequestAborted);
                            context.Response.StatusCode = StatusCodes.Status200OK;
                        });
                    }
                });
            });

        var server = new TestServer(builder);
        await Task.CompletedTask;
        return server;
    }

    private sealed class NoOpDemoDataSeeder : IDemoDataSeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
