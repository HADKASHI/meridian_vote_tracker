using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Meridian.Imports;
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

    private static JsonSerializerOptions CreateJson()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiHosting.ConfigureJson(json);
        return json;
    }
}
