using Client.Interfaces;
using Client.Services;

namespace Client
{
    public static class DependencyInjection
    {
        public static void AddBusinessLogicLayer(this IServiceCollection services)
        {
            services.AddScoped<NavigationService>();

#if ANDROID
            services.AddScoped<ILocalNetworkService, AndroidLocalNetworkService>();
            services.AddScoped<IStorageService, AndroidStorageService>();
#elif WINDOWS
            services.AddScoped<ILocalNetworkService, WindowsLocalNetworkService>();
            services.AddScoped<IStorageService, WindowsStorageService>();
#endif
        }
    }
}
