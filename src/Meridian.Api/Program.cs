using Meridian.Conflicts;
using Meridian.Extractions;
using Meridian.Imports;
using Meridian.Platform.Hosting;
using Meridian.Platform.Http;
using Meridian.Positions;
using Meridian.Snapshots;
using Meridian.Votes;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.AddMeridianApiPlatform();
builder.Services.AddOpenApi(o => o.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1);

// One feature module per area. A slice fills in its own module and never edits this file.
builder.Services
    .AddImportsModule()
    .AddConflictsModule()
    .AddVotesModule()
    .AddPositionsModule()
    .AddSnapshotsModule();

var app = builder.Build();
app.UseMeridianApiPlatform();
app.MapOpenApi().AllowAnonymous();
app.MapMeridianApiPlatform();

var api = app.MapGroup("/api").AddEndpointFilterFactory(RequestValidation.Factory);
api.MapImportsModule()
    .MapExtractionsModule()
    .MapConflictsModule()
    .MapVotesModule()
    .MapPositionsModule()
    .MapSnapshotsModule();

app.Run();

/// <summary>Visible to the integration tests' <c>WebApplicationFactory</c>.</summary>
public partial class Program;
