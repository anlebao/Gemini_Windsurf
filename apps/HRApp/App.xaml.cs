using Microsoft.Extensions.Logging;
using VanAn.HRApp.Application;
using Microsoft.Maui.ApplicationModel;

namespace VanAn.HRApp;

public partial class App : global::Microsoft.Maui.Controls.Application
{
	public App(IServiceProvider serviceProvider)
	{
		InitializeComponent();

		var tenantContext = serviceProvider.GetService<TenantContext>();
		var logger = serviceProvider.GetService<ILogger<App>>();

		MainPage = new AppShell();

		// Navigate to login page on startup
		Task.Run(async () =>
		{
			await Task.Delay(100); // Small delay for UI to initialize
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try
				{
					await Shell.Current.GoToAsync("//login");
				}
				catch (Exception ex)
				{
					logger?.LogError(ex, "Failed to navigate to login page");
				}
			});
		});
	}
}
