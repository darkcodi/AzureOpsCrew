using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
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

        // Pass OpenAI key from config if not already set
        if (string.IsNullOrWhiteSpace(options.OpenAiApiKey))
        {
            options.OpenAiApiKey = configuration["OpenAI:ApiKey"];
        }

        Log.Information("Running seeding default entities.");

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var seeder = new Seeder(context, options, passwordHasher);

            await seeder.Seed();
        }

        Log.Information("Seeding default entities succesfully complete..");
    }
}
