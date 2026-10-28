using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Http;
using Meridian.Platform.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Integration.Tests.Api;

/// <summary>
/// Generated from the API's endpoint metadata, so a new endpoint without a policy, or
/// with the wrong one, fails here without anyone writing a test for it.
/// </summary>
public sealed class RoleMatrixTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    /// <summary>The only endpoints anyone may call: the switcher must list users before one is chosen.</summary>
    private static readonly string[] Anonymous = ["GET /api/users"];

    /// <summary>Endpoints only the admin may call (POS, SNP publication).</summary>
    private static readonly string[] AdminOnly =
    [
        "PUT /api/votes/{voteId}/position",
        "POST /api/snapshots/{snapshotId}/publications",
    ];

    [Fact]
    public async Task Every_api_endpoint_requires_exactly_the_expected_policy()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var policies = ApiEndpoints(api).ToDictionary(e => e.Key, e => PolicyOf(e.Endpoint));

        Assert.All(policies, p => Assert.Equal(
            Anonymous.Contains(p.Key) ? "anonymous" : AdminOnly.Contains(p.Key) ? Policies.Admin : Policies.Analyst,
            p.Value));
        Assert.All(AdminOnly, key => Assert.Contains(key, policies.Keys));
    }

    [Fact]
    public async Task Every_endpoint_refuses_nobody_and_an_unknown_user_with_401()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        foreach (var endpoint in ApiEndpoints(api).Where(e => !Anonymous.Contains(e.Key)))
        foreach (var user in new string?[] { null, "mallory" })
        {
            var response = await SendAsync(api.ClientAs(user), endpoint);
            await response.AssertProblemAsync(401, "unknown-user");
        }
    }

    [Fact]
    public async Task Every_admin_endpoint_refuses_an_analyst_with_403()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        foreach (var endpoint in ApiEndpoints(api).Where(e => AdminOnly.Contains(e.Key)))
        foreach (var analyst in new[] { "dana", "omar" })
        {
            var response = await SendAsync(api.ClientAs(analyst), endpoint);
            await response.AssertProblemAsync(403, "forbidden");
        }
    }

    [Fact]
    public async Task Every_stub_answers_the_right_role_with_501_not_implemented()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        foreach (var endpoint in ApiEndpoints(api).Where(e => e.Endpoint.Metadata.GetMetadata<StubMetadata>() is not null))
        {
            var user = AdminOnly.Contains(endpoint.Key) ? "lena" : "dana";
            var response = await SendAsync(api.ClientAs(user), endpoint, validBody: true);
            Assert.True(response.StatusCode == HttpStatusCode.NotImplemented,
                $"{endpoint.Key} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
            await response.AssertProblemAsync(501, "not-implemented");
        }
    }

    private sealed record ApiEndpoint(string Key, string Method, RouteEndpoint Endpoint);

    private static IEnumerable<ApiEndpoint> ApiEndpoints(ApiFactory api) =>
        api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(m =>
                new ApiEndpoint($"{m} {Template(e)}", m, e)));

    /// <summary>The route as the contract shows it: <c>/api/imports/{importId}</c>, without constraints or a trailing slash.</summary>
    private static string Template(RouteEndpoint endpoint) =>
        Regex.Replace(endpoint.RoutePattern.RawText!, @"\{(\w+):\w+\}", "{$1}").TrimEnd('/');

    private static string PolicyOf(Endpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null) return "anonymous";
        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).Distinct().ToList();
        return policies.Count == 1 ? policies[0] ?? "(no policy)" : $"({string.Join(", ", policies)})";
    }

    /// <summary>Route values and bodies that bind, so a stub is reached past binding and validation.</summary>
    private static Task<HttpResponseMessage> SendAsync(HttpClient client, ApiEndpoint endpoint, bool validBody = false)
    {
        var path = Template(endpoint.Endpoint)
            .Replace("{stateCode}", "FRA")
            .Replace("{importId}", "1").Replace("{conflictId}", "1").Replace("{voteId}", "1").Replace("{snapshotId}", "1");
        if (endpoint.Key == "GET /api/snapshots/compare") path += "?a=1&b=2";

        var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), path);
        if (endpoint.Method is "POST" or "PUT")
            request.Content = BodyFor(endpoint.Key, validBody);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static HttpContent BodyFor(string key, bool valid)
    {
        if (key == "POST /api/imports")
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent("assembly_vote_id\n"u8.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            form.Add(file, "file", "export.csv");
            return form;
        }

        var json = !valid ? "{}" : key switch
        {
            "POST /api/conflicts/{conflictId}/decision" => """{"decision":"accept","comment":"ok"}""",
            "PUT /api/votes/{voteId}/position" => """{"stance":"yes","weight":3,"seenRevisionId":null}""",
            "POST /api/snapshots/preview" => """{"from":"2024-01-01","to":"2024-03-31"}""",
            "POST /api/snapshots" => """{"name":"Q1","selection":{"sessions":[79]}}""",
            "POST /api/snapshots/{snapshotId}/publications" => """{"quarter":"2024-Q1"}""",
            _ => throw new InvalidOperationException($"No sample body for {key}: add one here."),
        };
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
