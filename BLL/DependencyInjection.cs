using BLL.Interfaces;
using BLL.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BLL
{
    public static class DependencyInjection
    {
        public static void AddBusinessLogicLayer(this IServiceCollection services)
        {
            services.AddScoped<ILocalNetworkService, LocalNetworkService>();
        }
    }
}
