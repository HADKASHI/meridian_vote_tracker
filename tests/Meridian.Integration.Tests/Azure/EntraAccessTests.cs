using Azure.Core;
using Meridian.Integration.Tests.Infrastructure;
using Meridian.Platform.Hosting;
using Meridian.Platform.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Azure.Storage.Blobs;

namespace Meridian.Integration.Tests.Azure;

/// <summary>
/// Blob storage can be reached with a Microsoft Entra ID sign-in instead of a key (<c>az login</c> locally, an identity
/// in Azure). The real sign-in is the operator's; here a fake credential stands in.
/// </summary>
public sealed class EntraAccessTests(AzuriteFixture azurite)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeCredential(string token = "meridian_test", Exception? failure = null) : TokenCredential
    {
        public readonly List<string[]> Scopes = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            lock (Scopes) Scopes.Add(requestContext.Scopes);
            return failure is null ? new(new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1))) : throw failure;
        }
    }

    // ---- Blob ----

    [Fact]
    public void A_service_uri_is_reached_with_the_credential_and_wins_over_a_connection_string()
    {
        var credential = new FakeCredential();

        var service = BlobAccess.Service(azurite.ConnectionString, "https://dotnetcandidate8st.blob.core.windows.net/", credential);

        Assert.Equal("dotnetcandidate8st", service.AccountName);
        Assert.Equal(new Uri("https://dotnetcandidate8st.blob.core.windows.net/"), service.Uri);
    }

    [Fact]
    public void Without_a_service_uri_the_connection_string_is_used_as_before()
    {
        var service = BlobAccess.Service(azurite.ConnectionString, "  ", new FakeCredential());

        Assert.Equal("devstoreaccount1", service.AccountName);
    }

    [Fact]
    public void Neither_setting_is_named_when_both_are_missing()
    {
        var error = Assert.Throws<InvalidOperationException>(() => BlobAccess.Service(null, null, new FakeCredential()));

        Assert.Contains("Blob:ServiceUri", error.Message);
        Assert.Contains("Blob:ConnectionString", error.Message);
    }

    [Fact]
    public void The_app_gets_its_container_from_the_service_uri_when_that_is_configured()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Blob:ServiceUri"] = "https://dotnetcandidate8st.blob.core.windows.net/",
            ["Blob:Container"] = "imports",
        }).Build();
        using var provider = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddLogging()
            .AddMeridianBlobStorage().BuildServiceProvider();

        var container = provider.GetRequiredService<BlobContainerClient>();

        Assert.Equal(new Uri("https://dotnetcandidate8st.blob.core.windows.net/imports"), container.Uri);
    }

    [Fact]
    public async Task The_startup_check_names_the_blob_account_when_the_sign_in_fails()
    {
        var credential = new FakeCredential(failure: new InvalidOperationException("Please run 'az login'."));

        var failure = Assert.Single(await ConnectionCheck.RunAsync(
            null, null, requireTls: false, Ct, blobServiceUri: "https://nosuchaccount.blob.core.windows.net/", credential: credential));

        Assert.Equal("blob", failure.Service);
        Assert.Contains("nosuchaccount", failure.Message);
        Assert.Contains("Microsoft Entra ID", failure.Message);
    }

    [Fact]
    public async Task A_sign_in_that_times_out_is_a_failure_and_not_a_crash()
    {
        var credential = new FakeCredential(failure: new TaskCanceledException("A task was canceled."));

        var failure = Assert.Single(await ConnectionCheck.RunAsync(
            null, null, requireTls: false, Ct, blobServiceUri: "https://nosuchaccount.blob.core.windows.net/", credential: credential));

        Assert.Equal("blob", failure.Service);
    }

    [Fact]
    public async Task The_startup_check_says_so_when_no_blob_setting_exists_at_all()
    {
        var failure = Assert.Single(await ConnectionCheck.RunAsync(null, null, requireTls: false, Ct, checkBlob: true));

        Assert.Equal("blob", failure.Service);
        Assert.Contains("Blob__ServiceUri", failure.Message);
    }
}
