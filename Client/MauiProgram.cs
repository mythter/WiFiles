using System.Reflection;
using Blazored.LocalStorage;
using Client.Interfaces;
using Client.Services;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Client
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            using var appsettingsStream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream("Client.wwwroot.appsettings.json");

            if (appsettingsStream != null)
            {
                var config = new ConfigurationBuilder()
                    .AddJsonStream(appsettingsStream)
                    .Build();

                builder.Configuration.AddConfiguration(config);
            }

            builder.Services.AddMauiBlazorWebView();

            builder.Services.ConfigureServices();

            builder.Services.AddBlazoredLocalStorage();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();

            AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
            {
                Console.WriteLine("********** UNHANDLED EXCEPTION **********");
                Console.WriteLine($"Message: {e.Exception.Message}");
                Console.WriteLine($"Stack trace: {e.Exception.StackTrace}");
            };
#endif

            return builder.Build();
        }

        /// <summary>
        /// Extension method for configuring custom services
        /// </summary>
        static void ConfigureServices(this IServiceCollection services)
        {
            services.AddScoped<NavigationService>();
            services.AddScoped<ILocalNetworkService, LocalNetworkService>();

            services.AddSingleton<LocalTransferService>();
            services.AddSingleton<GlobalTransferService>();

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
