using Meridian.Imports;
using Meridian.Platform.Hosting;
using Meridian.Snapshots;

var builder = Host.CreateApplicationBuilder(args);
builder.AddMeridianWorkerPlatform();

// Slices plug in their job handler inside their own module.
builder.Services
    .AddImportsJobs()
    .AddSnapshotsJobs();

builder.Build().Run();
