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

            serviceCollection.AddHttpClient<OrToolsScheduleOptimizationClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["OrToolsApi:BaseUrl"]!);
            });
            serviceCollection.AddKeyedScoped<IScheduleOptimizationClient>(
                OptimizationClients.OrTools,
                (sp, _) => sp.GetRequiredService<OrToolsScheduleOptimizationClient>()
            );

            serviceCollection.AddHttpClient<TimefoldScheduleOptimizationClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["TimefoldApi:BaseUrl"]!);
            });
            serviceCollection.AddKeyedScoped<IScheduleOptimizationClient>(
                OptimizationClients.Timefold,
                (sp, _) => sp.GetRequiredService<TimefoldScheduleOptimizationClient>()
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
