using System.Security.Claims;
using System.Text.Encodings.Web;
using Meridian.Data;
using Meridian.Platform.Problems;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meridian.Platform.Identity;

/// <summary>
/// Signs in the seeded user named by the <c>X-User-Id</c> header, with their role as a
/// claim. This is the only piece real sign-in (Entra ID, JWT) would replace; endpoints
/// see a standard principal and ordinary policies.
/// </summary>
public sealed class UserHeaderHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    MeridianDbContext db,
    IProblemDetailsService problems)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "UserHeader";
    public const string HeaderName = "X-User-Id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var header) || string.IsNullOrWhiteSpace(header))
            return AuthenticateResult.NoResult();

        var id = header.ToString();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, Context.RequestAborted);
        if (user is null) return AuthenticateResult.Fail("Unknown user.");

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.Role, SnakeCaseEnumConverter<Domain.Role>.ToName(user.Role)),
            ],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(StatusCodes.Status401Unauthorized, ProblemTypes.UnknownUser,
            "Unknown user", $"Send the {HeaderName} header with a known user id.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(StatusCodes.Status403Forbidden, ProblemTypes.Forbidden,
            "Forbidden", "Your role does not allow this action.");

    private async Task WriteProblemAsync(int status, string type, string title, string detail)
    {
        Response.StatusCode = status;
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = new ProblemDetails { Status = status, Type = type, Title = title, Detail = detail },
        });
    }
}

public static class PrincipalExtensions
{
    /// <summary>The signed-in user's id. Only call on endpoints that require a policy.</summary>
    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("No signed-in user.");
}
