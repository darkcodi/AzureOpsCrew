using AzureOpsCrew.Infrastructure.Db;
using Serilog;

namespace AzureOpsCrew.Api.Setup.Seeds;

public static class ServiceProviderExtensions
{
    public static async Task RunSeeding(this IServiceProvider provider, IConfiguration configuration)
    {
        var options = configuration.GetRequiredSection("Seeding").Get<SeederOptions>()!;

        if (!options.IsEnabled)
        {
            Log.Information("Seeding is not enabled");
            return;
        }

        Log.Information("Running seeding default entities.");

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();
            var seeder = new Seeder(context, options);

            await seeder.Seed();
        }

        Log.Information("Seeding default entities succesfully complete..");
    }
}
