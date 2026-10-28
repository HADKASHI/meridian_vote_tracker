using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Meridian.Integration.Tests.Infrastructure;

/// <summary>The API in process, against its own database copied from the template and Azurite.</summary>
public sealed class ApiFactory(string connectionString, string blobConnectionString) : WebApplicationFactory<Program>
{
    public const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=none;Username=x;Password=x;Timeout=1";

    public static async Task<ApiFactory> CreateAsync(PostgresFixture postgres, AzuriteFixture azurite) =>
        new(await postgres.CreateDatabaseAsync(), azurite.ConnectionString);

    public string ConnectionString => connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Meridian"] = connectionString,
            ["Blob:ConnectionString"] = blobConnectionString,
            ["Blob:Container"] = "imports",
        }));
    }

    /// <summary>A client calling as <paramref name="userId"/>, or as nobody when it is null.</summary>
    public HttpClient ClientAs(string? userId)
    {
        var client = CreateClient();
        if (userId is not null) client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }
}

public static class ProblemAssertions
{
    /// <summary>Asserts a ProblemDetails response of the given status and type, carrying a traceId.</summary>
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, int status, string type)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(type, problem.GetProperty("type").GetString());
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        return problem;
    }
}
