using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Meridian.Data;
using Meridian.Domain;
using Meridian.Platform.Health;
using Meridian.Platform.Identity;
using Meridian.Platform.Jobs;
using Meridian.Platform.Problems;
using Meridian.Platform.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Meridian.Platform.Hosting;

/// <summary>Everything the API shares across feature modules: data, identity, errors, JSON, health, storage, logs.</summary>
public static class ApiHosting
{
    public static void AddMeridianApiPlatform(this WebApplicationBuilder builder)
    {
        builder.AddMeridianObservability();
        var services = builder.Services;

        services.AddMeridianDb();
        services.AddMeridianBlobStorage();
        services.AddMeridianStartupConnectionCheck();
        services.AddMeridianProblemDetails();
        services.AddMeridianHealthChecks();
        services.ConfigureHttpJsonOptions(o => ConfigureJson(o.SerializerOptions));

        services.AddAuthentication(UserHeaderHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, UserHeaderHandler>(UserHeaderHandler.SchemeName, null);
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Analyst, p => p.RequireRole(RoleName(Role.Analyst), RoleName(Role.Admin)))
            .AddPolicy(Policies.Admin, p => p.RequireRole(RoleName(Role.Admin)));

        // nginx in front forwards the client address and scheme; nothing else reaches the API.
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });
    }

    private static string RoleName(Role role) => SnakeCaseEnumConverter<Role>.ToName(role);

    /// <summary>
    /// camelCase names, enums as their glossary words, and strict numbers, so the
    /// generated client types numbers as <c>number</c>, never <c>number | string</c>.
    /// </summary>
    public static void ConfigureJson(JsonSerializerOptions json)
    {
        json.NumberHandling = JsonNumberHandling.Strict;
        json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
    }

    public static void UseMeridianApiPlatform(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Meridian.Requests");
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            using (userId is null ? null : logger.BeginScope(new Dictionary<string, object> { ["UserId"] = userId }))
                await next(context);
        });
        app.UseAuthorization();
    }

    public static IEndpointRouteBuilder MapMeridianApiPlatform(this IEndpointRouteBuilder app)
    {
        app.MapMeridianHealthChecks();
        app.MapUsers();
        app.MapGroup("/api").MapJobs();
        return app;
    }
}
