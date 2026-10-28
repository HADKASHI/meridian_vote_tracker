using Testcontainers.Azurite;

[assembly: AssemblyFixture(typeof(Meridian.Integration.Tests.Infrastructure.AzuriteFixture))]

namespace Meridian.Integration.Tests.Infrastructure;

/// <summary>One Azurite container per test run, the same version as compose.</summary>
public sealed class AzuriteFixture : IAsyncLifetime
{
    private readonly AzuriteContainer _container = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0")
        .WithCommand("--skipApiVersionCheck")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
