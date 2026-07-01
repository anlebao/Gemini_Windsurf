using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace VanAn.Architecture.Tests;

/// <summary>
/// Wave 12: Authorization enforcement tests.
/// Uses reflection to verify that API controllers have [Authorize] attributes at the class
/// or method level, and that specific write operations are not left [AllowAnonymous].
/// These are lightweight, no-server tests — no DI startup, no network calls.
/// </summary>
[Trait("Category", "Authorization")]
public class AuthorizationEnforcementTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────────────

    private static IEnumerable<Type> GetControllers(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract
                        && (t.IsSubclassOf(typeof(ControllerBase)) || t.GetCustomAttribute<ApiControllerAttribute>() != null));

    private static bool HasClassLevelAuthorize(Type controller) =>
        controller.GetCustomAttribute<AuthorizeAttribute>() != null;

    private static bool HasClassLevelAllowAnonymous(Type controller) =>
        controller.GetCustomAttribute<AllowAnonymousAttribute>() != null;

    private static bool HasMethodLevelAuth(MethodInfo method) =>
        method.GetCustomAttribute<AuthorizeAttribute>() != null;

    private static bool IsAllowAnonymous(MethodInfo method) =>
        method.GetCustomAttribute<AllowAnonymousAttribute>() != null;

    private static bool IsHttpActionMethod(MethodInfo method) =>
        method.GetCustomAttribute<HttpGetAttribute>() != null
        || method.GetCustomAttribute<HttpPostAttribute>() != null
        || method.GetCustomAttribute<HttpPutAttribute>() != null
        || method.GetCustomAttribute<HttpDeleteAttribute>() != null
        || method.GetCustomAttribute<HttpPatchAttribute>() != null
        || method.GetCustomAttribute<RouteAttribute>() != null;

    private static IEnumerable<MethodInfo> GetActionMethods(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(IsHttpActionMethod);

    // ── Gateway Authorization Tests ───────────────────────────────────────────────────────

    private static Assembly GatewayAssembly =>
        // Force-load the assembly if not yet in the AppDomain (e.g., when no type from it has been used)
        AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "VanAn.Gateway")
        ?? Assembly.Load("VanAn.Gateway");

    [Fact(DisplayName = "W12-G1: BuildController must have class-level [Authorize]")]
    public void BuildController_MustHaveAuthorize()
    {
        var controller = GatewayAssembly.GetTypes()
            .Single(t => t.Name == "BuildController");

        Assert.True(HasClassLevelAuthorize(controller),
            "BuildController is missing [Authorize] at class level — Wave 12 fix required.");
    }

    [Fact(DisplayName = "W12-G2: LocalizationController must have class-level [Authorize]")]
    public void LocalizationController_MustHaveAuthorize()
    {
        var controller = GatewayAssembly.GetTypes()
            .Single(t => t.Name == "LocalizationController");

        Assert.True(HasClassLevelAuthorize(controller),
            "LocalizationController is missing [Authorize] at class level — Wave 12 fix required.");
    }

    [Fact(DisplayName = "W12-G3: OnboardingController must have class-level [Authorize]")]
    public void OnboardingController_MustHaveAuthorize()
    {
        var controller = GatewayAssembly.GetTypes()
            .Single(t => t.Name == "OnboardingController");

        Assert.True(HasClassLevelAuthorize(controller),
            "OnboardingController is missing [Authorize] at class level — Wave 12 fix required.");
    }

    [Fact(DisplayName = "W12-G4: ShopConfigController must have class-level [Authorize]")]
    public void ShopConfigController_MustHaveAuthorize()
    {
        var controller = GatewayAssembly.GetTypes()
            .Single(t => t.Name == "ShopConfigController");

        Assert.True(HasClassLevelAuthorize(controller),
            "ShopConfigController is missing [Authorize] at class level — Wave 12 fix required.");
    }

    [Fact(DisplayName = "W12-G5: VietQrController must have class-level [Authorize]")]
    public void VietQrController_MustHaveAuthorize()
    {
        var controller = GatewayAssembly.GetTypes()
            .Single(t => t.Name == "VietQrController");

        Assert.True(HasClassLevelAuthorize(controller),
            "VietQrController is missing [Authorize] at class level — Wave 12 fix required.");
    }

    [Fact(DisplayName = "W12-G6: VoiceCommandController must have class-level [Authorize]")]
    public void VoiceCommandController_MustHaveAuthorize()
    {
        var controller = GatewayAssembly.GetTypes()
            .Single(t => t.Name.StartsWith("VoiceCommandController", StringComparison.Ordinal));

        Assert.True(HasClassLevelAuthorize(controller),
            "VoiceCommandController is missing [Authorize] at class level — Wave 12 fix required.");
    }

    [Fact(DisplayName = "W12-G7: All Gateway controllers must have class-level [Authorize] (except WebhookController)")]
    public void AllGatewayControllers_ExceptWebhook_MustHaveClassLevelAuthorize()
    {
        // Excluded: WebhookController uses [AllowAnonymous] on specific methods intentionally (external provider callbacks)
        // Excluded: CampaignsController and PublicOrdersController use [AllowAnonymous] for public guest access (Wave 16)
        // Excluded: Wave 17 customer-facing controllers use [AllowAnonymous] with X-Customer-Token header auth
        var exemptControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WebhookController",
            "CampaignsController",
            "PublicOrdersController",
            // Wave 17: Customer retention endpoints (OTP, Loyalty, Orders, Stores, Notifications)
            "CustomersController",
            "LoyaltyController",
            "CustomerOrdersController",
            "ShopsController",
            "NotificationsController",
            // Wave 4: Platform-level SystemAdmin endpoint — method-level [Authorize(Policy="SystemAdmin")] with Bearer scheme
            "TenantOnboardingController"
        };

        var controllers = GetControllers(GatewayAssembly)
            .Where(t => !exemptControllers.Contains(t.Name))
            .ToList();

        var unprotected = controllers
            .Where(t => !HasClassLevelAuthorize(t))
            .Select(t => t.Name)
            .ToList();

        Assert.True(unprotected.Count == 0,
            $"The following Gateway controllers are missing class-level [Authorize]: {string.Join(", ", unprotected)}");
    }

    // ── ShopERP Authorization Tests ───────────────────────────────────────────────────────

    private static Assembly ShopErpAssembly =>
        AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "VanAn.ShopERP")
        ?? Assembly.Load("VanAn.ShopERP");

    [Fact(DisplayName = "W12-S1: ShopsController write operations must NOT be [AllowAnonymous]")]
    public void ShopsController_WriteOperations_MustNotBeAllowAnonymous()
    {
        var controller = ShopErpAssembly.GetTypes()
            .Single(t => t.Name == "ShopsController");

        // POST (Create), PUT (Update), DELETE (Delete) must require auth
        var writeMethods = GetActionMethods(controller)
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null
                        || m.GetCustomAttribute<HttpPutAttribute>() != null
                        || m.GetCustomAttribute<HttpDeleteAttribute>() != null)
            .ToList();

        var anonymousWrites = writeMethods
            .Where(m => IsAllowAnonymous(m))
            .Select(m => m.Name)
            .ToList();

        Assert.True(anonymousWrites.Count == 0,
            $"ShopsController write methods should not be [AllowAnonymous]. Found: {string.Join(", ", anonymousWrites)}");
    }

    [Fact(DisplayName = "W12-S2: OrderWorkflowController TransitionStatus must NOT be [AllowAnonymous]")]
    public void OrderWorkflowController_TransitionStatus_MustNotBeAllowAnonymous()
    {
        var controller = ShopErpAssembly.GetTypes()
            .Single(t => t.Name == "OrderWorkflowController");

        var transitionMethod = GetActionMethods(controller)
            .SingleOrDefault(m => m.Name == "TransitionStatus"
                                  || m.GetCustomAttribute<HttpPutAttribute>() != null);

        Assert.NotNull(transitionMethod);
        Assert.False(IsAllowAnonymous(transitionMethod),
            "OrderWorkflowController.TransitionStatus (PUT) must not be [AllowAnonymous] — Wave 12 fix required.");
    }

    [Fact(DisplayName = "W12-S3: All ShopERP controllers must have class-level or all-method-level [Authorize] (except DevLoginController)")]
    public void AllShopErpControllers_MustHaveAuthCoverage()
    {
        // Excluded:
        // - DevLoginController: dev-only, guarded in Program.cs by environment check
        // - Wave 17: Customer retention endpoints use [AllowAnonymous] with X-Customer-Token header auth
        var exemptControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DevLoginController",
            // Wave 17: Customer retention endpoints (OTP, Loyalty, Orders, Notifications)
            "CustomerIdentityController",
            "CustomerOrdersController",
            "LoyaltyController",
            "NotificationsController"
        };

        var controllers = GetControllers(ShopErpAssembly)
            .Where(t => !exemptControllers.Contains(t.Name))
            .ToList();

        var fullyUnprotected = new List<string>();

        foreach (var controller in controllers)
        {
            bool classLevelAuth = HasClassLevelAuthorize(controller);
            if (classLevelAuth) continue; // protected at class level

            // Check all action methods have at least one [Authorize] or the class has it
            var actions = GetActionMethods(controller).ToList();
            if (actions.Count == 0) continue; // no actions = skip

            bool allMethodsHaveAuth = actions.All(m => HasMethodLevelAuth(m) || IsAllowAnonymous(m));
            if (!allMethodsHaveAuth)
            {
                var unprotectedMethods = actions
                    .Where(m => !HasMethodLevelAuth(m) && !IsAllowAnonymous(m))
                    .Select(m => m.Name);
                fullyUnprotected.Add($"{controller.Name}.[{string.Join(",", unprotectedMethods)}]");
            }
        }

        Assert.True(fullyUnprotected.Count == 0,
            $"The following ShopERP controller actions have neither [Authorize] nor [AllowAnonymous]: {string.Join("; ", fullyUnprotected)}");
    }
}
