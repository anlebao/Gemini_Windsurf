using Microsoft.Extensions.Logging;
using VanAn.HRApp.Application;
using Microsoft.Extensions.Configuration;

namespace VanAn.HRApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Add Configuration
		var config = new ConfigurationBuilder()
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
			.Build();

		builder.Configuration.AddConfiguration(config);

		// Register services for ecoNexus integration
		builder.Services.AddSingleton<HttpClient>();
		builder.Services.AddSingleton<AuthService>();
		builder.Services.AddSingleton<TenantContext>();
		
		// Register pages
		builder.Services.AddTransient<UI.LoginPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
