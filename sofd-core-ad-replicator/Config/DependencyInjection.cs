using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using sofd_core_ad_replicator.Jobs;
using sofd_core_ad_replicator.Services.ActiveDirectory;
using sofd_core_ad_replicator.Services.Sofd;

namespace sofd_core_ad_replicator.Config
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            // Bind settings
            var settings = new Settings();
            configuration.Bind(settings);
            services.AddSingleton(settings);

            // Add other required services
            services.AddSingleton<ActiveDirectoryService>();
            services.AddSingleton<SofdService>();
            services.AddSingleton<SyncService>();

            return services;
        }

    }
}
