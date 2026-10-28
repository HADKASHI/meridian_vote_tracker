using Meridian.Data;
using Meridian.Fixtures;
using Meridian.Imports;
using Meridian.Platform.Hosting;
using Meridian.Snapshots;

var builder = Host.CreateApplicationBuilder(args);
builder.AddMeridianWorkerPlatform();

// Slices plug in their job handler inside their own module.
builder.Services
    .AddImportsJobs()
    .AddSnapshotsJobs();

var host = builder.Build();

if (args is ["seed-demo"])
{
    // Synthetic demo data for checking screens by hand: dotnet run --project src/Meridian.Worker -- seed-demo
    if (!host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
    {
        Console.Error.WriteLine("seed-demo runs only in the Development environment.");
        return 1;
    }
    await using var scope = host.Services.CreateAsyncScope();
    await DemoSeed.RunAsync(scope.ServiceProvider.GetRequiredService<MeridianDbContext>());
    Console.WriteLine("Demo data seeded.");
    return 0;
}

host.Run();
return 0;
