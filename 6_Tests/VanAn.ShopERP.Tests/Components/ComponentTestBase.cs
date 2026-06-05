using VanAn.UI.Platform.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services;
using Bunit;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Adapters;
using Microsoft.AspNetCore.Components;
using Bunit.JSInterop;
using VanAn.UI.Platform.Components;

namespace VanAn.ShopERP.Tests.Components;

/// <summary>
/// Base class for component tests with common service registrations
/// </summary>
public class ComponentTestBase : TestContext
{
    public ComponentTestBase()
    {
        // Register UI Platform services
        Services.AddSingleton<IThemeProvider, ThemeProvider>();
        Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
        
        // Register Authentication with tenant ID
        Services.AddSingleton<AuthenticationStateProvider, TestAuthenticationStateProvider>();
        
        // Register Logging
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        
        // Register Core Services (mock by default, tests can override)
        Services.AddSingleton<IAccountingService>(sp => new Mock<IAccountingService>().Object);
        
        // Configure JSInterop for components with JavaScript interop
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Register UI Platform components
        Services.AddSingleton<VanALayout>();
        Services.AddSingleton<VanANavigation>();

        // Bunit automatically discovers components from referenced assemblies
        // UI.Platform is already referenced in the project, so components should be discoverable
    }
}
