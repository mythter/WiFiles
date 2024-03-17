using BLL.Interfaces;
using BLL.Services;
using Client.Services;

namespace BLL
{
    public static class DependencyInjection
    {
        public static void AddBusinessLogicLayer(this IServiceCollection services)
        {
            services.AddScoped<NavigationService>();

#if ANDROID
            services.AddScoped<ILocalNetworkService, AndroidLocalNetworkService>();
#elif WINDOWS
            services.AddScoped<ILocalNetworkService, WindowsLocalNetworkService>();
#endif
        }
    }
}
