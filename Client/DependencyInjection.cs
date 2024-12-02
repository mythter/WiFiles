using Client.Interfaces;
using Client.Services;

namespace Client
{
    public static class DependencyInjection
    {
        public static void AddBusinessLogicLayer(this IServiceCollection services)
        {
            services.AddScoped<NavigationService>();
            services.AddScoped<ILocalNetworkService, LocalNetworkService>();

            services.AddSingleton<LocalTransferService>();

#if ANDROID
            services.AddSingleton<INetworkInfoService, AndroidNetworkInfoService>();
            services.AddSingleton<IDeviceService, AndroidDeviceService>();

            services.AddSingleton<IStorageService, AndroidStorageService>();
#elif WINDOWS
            services.AddSingleton<INetworkInfoService, WindowsNetworkInfoService>();
            services.AddSingleton<IDeviceService, WindowsDeviceService>();

            services.AddSingleton<IStorageService, WindowsStorageService>();
#endif
        }
    }
}
