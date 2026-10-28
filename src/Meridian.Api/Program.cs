using Meridian.Platform.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddMeridianApiPlatform();

var app = builder.Build();
app.UseMeridianApiPlatform();
app.MapMeridianApiPlatform();
app.Run();

/// <summary>Visible to the integration tests' <c>WebApplicationFactory</c>.</summary>
public partial class Program;
