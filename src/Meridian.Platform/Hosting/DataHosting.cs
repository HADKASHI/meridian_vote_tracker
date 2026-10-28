using Meridian.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Platform.Hosting;

public static class DataHosting
{
    public static IServiceCollection AddMeridianDb(this IServiceCollection services) =>
        // Read when the context is first built, so configuration added by a test host applies.
        services.AddDbContext<MeridianDbContext>((provider, options) => MeridianDb.Configure(options,
            provider.GetRequiredService<IConfiguration>().GetConnectionString("Meridian")
                ?? throw new InvalidOperationException("ConnectionStrings:Meridian is not set.")));
}
