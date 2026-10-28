using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Meridian.Imports;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Hosting;

namespace Meridian.Integration.Tests.Imports;

/// <summary>Calls the imports endpoints the way the Angular client does.</summary>
public static class ImportApi
{
    public static readonly JsonSerializerOptions Json = CreateJson();

    public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName, byte[] content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/imports", form, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName, string content) =>
        UploadAsync(client, fileName, Encoding.UTF8.GetBytes(content));

    public static async Task<ImportCreatedDto> UploadOkAsync(HttpClient client, string fileName, string content)
    {
        var response = await UploadAsync(client, fileName, content);
        Assert.Equal(202, (int)response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImportCreatedDto>(Json, TestContext.Current.CancellationToken))!;
    }

    public static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode,
            $"GET {url} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        return (await response.Content.ReadFromJsonAsync<T>(Json, TestContext.Current.CancellationToken))!;
    }

    /// <summary>The worker with the import job handler, on the API's database and blob container.</summary>
    public static Microsoft.Extensions.Hosting.IHost Worker(ApiFactory api, IDictionary<string, string?>? settings = null)
    {
        var all = new Dictionary<string, string?>
        {
            ["Blob:ConnectionString"] = api.BlobConnectionString,
            ["Blob:Container"] = api.BlobContainer,
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>()) all[key] = value;
        return WorkerHost.Build(api.ConnectionString, [], all, services => services.AddImportsJobs());
    }

    /// <summary>Runs every queued job to the end, as the worker would.</summary>
    public static async Task RunJobsAsync(ApiFactory api)
    {
        using var worker = Worker(api);
        await worker.Processor().RunUntilIdleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Uploads <paramref name="content"/> as dana, runs the import and returns its report.</summary>
    public static async Task<ImportDto> ImportAsync(ApiFactory api, string fileName, string content, string user = "dana") =>
        await ImportAsync(api, fileName, Encoding.UTF8.GetBytes(content), user);

    public static async Task<ImportDto> ImportAsync(ApiFactory api, string fileName, byte[] content, string user = "dana")
    {
        var client = api.ClientAs(user);
        var response = await UploadAsync(client, fileName, content);
        Assert.Equal(202, (int)response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ImportCreatedDto>(Json, TestContext.Current.CancellationToken))!;
        await RunJobsAsync(api);
        return await GetAsync<ImportDto>(client, $"/api/imports/{created.ImportId}");
    }

    /// <summary>Imports a committed fixture from <c>Imports/Files</c>.</summary>
    public static Task<ImportDto> ImportFixtureAsync(ApiFactory api, string fileName, string user = "dana") =>
        ImportAsync(api, fileName, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Imports", "Files", fileName)), user);

    private static JsonSerializerOptions CreateJson()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiHosting.ConfigureJson(json);
        return json;
    }
}
