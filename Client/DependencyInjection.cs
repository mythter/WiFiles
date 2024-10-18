using Client.Interfaces;
using Client.Services;

namespace Client
{
    public static class DependencyInjection
    {
        public static void AddBusinessLogicLayer(this IServiceCollection services)
        {
            services.AddScoped<NavigationService>();

            services.AddSingleton<LocalTransferService>();

#if ANDROID
            services.AddScoped<ILocalNetworkService, AndroidLocalNetworkService>();
            services.AddSingleton<IDeviceService, AndroidDeviceService>();

            services.AddScoped<IStorageService, AndroidStorageService>();
#elif WINDOWS
            services.AddScoped<ILocalNetworkService, WindowsLocalNetworkService>();
            services.AddSingleton<IDeviceService, WindowsDeviceService>();

            services.AddScoped<IStorageService, WindowsStorageService>();
#endif
        }
    }
}
