using System.Text.Json;
using Meridian.Integration.Tests.Infrastructure;

namespace Meridian.Integration.Tests.Api;

public sealed class OpenApiTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    [Fact]
    public async Task Numbers_are_plain_numbers_in_the_contract()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var document = await api.ClientAs(null).GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.StartsWith("3.1", JsonDocument.Parse(document).RootElement.GetProperty("openapi").GetString());
        Assert.DoesNotContain("\"pattern\": \"^-?(?:0|[1-9]\\\\d*)$\"", document);
        Assert.DoesNotContain("\"string\",\n            \"integer\"", document);
    }

    [Fact]
    public async Task Every_problem_type_of_the_catalogue_has_its_typed_body_in_the_contract()
    {
        await using var api = await ApiFactory.CreateAsync(postgres, azurite);

        var document = JsonDocument.Parse(await api.ClientAs(null).GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        foreach (var (schema, extension) in new[]
                 {
                     ("AlreadyDecidedProblem", "decision"), ("StalePositionProblem", "current"),
                     ("QuarterPublishedProblem", "publication"), ("NoPositionProblem", "votes"),
                     ("ValidationProblem", "errors"), ("Problem", "traceId"),
                 })
            Assert.True(schemas.GetProperty(schema).GetProperty("properties").TryGetProperty(extension, out _),
                $"{schema} lacks {extension}");
    }
}
