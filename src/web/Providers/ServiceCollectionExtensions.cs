using Web.Providers.Clients;

namespace Web.Providers
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterScheduleOptimizationClients(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            serviceCollection.AddHttpClient<SpecializedScheduleOptimizationClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["SpecializedApi:BaseUrl"]!);
            });
            serviceCollection.AddKeyedScoped<IScheduleOptimizationClient>(
                OptimizationClients.Specialized,
                (sp, _) => sp.GetRequiredService<SpecializedScheduleOptimizationClient>()
            );

            return serviceCollection;
        }

        public enum OptimizationClients
        {
            Specialized,
            OrTools,
            Timefold
        }
    }
}
