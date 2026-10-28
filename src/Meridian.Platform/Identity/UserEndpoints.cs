using System.Security.Claims;
using Meridian.Data;
using Meridian.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Platform.Identity;

public sealed record UserDto(string Id, string DisplayName, Role Role);

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUsers(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users").WithTags("Users");

        // Anonymous: the switcher lists users before anyone is chosen.
        users.MapGet("", async Task<Ok<UserDto[]>> (MeridianDbContext db, CancellationToken ct) =>
                TypedResults.Ok(await db.Users.AsNoTracking().OrderBy(u => u.Id)
                    .Select(u => new UserDto(u.Id, u.DisplayName, u.Role)).ToArrayAsync(ct)))
            .WithName("listUsers")
            .AllowAnonymous();

        users.MapGet("/me", async Task<Ok<UserDto>> (ClaimsPrincipal principal, MeridianDbContext db, CancellationToken ct) =>
            {
                var id = principal.UserId();
                return TypedResults.Ok(await db.Users.AsNoTracking().Where(u => u.Id == id)
                    .Select(u => new UserDto(u.Id, u.DisplayName, u.Role)).SingleAsync(ct));
            })
            .WithName("getCurrentUser")
            .RequireAuthorization(Policies.Analyst);

        return app;
    }
}
