using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Reflection;

// Target framework attribute - prevent auto-generation conflicts
[assembly: TargetFramework(".NETCoreApp,Version=v8.0")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("VanAn.Shared")]
[assembly: AssemblyCopyright("Copyright ©  2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Friend assemblies for protected internal access
[assembly: InternalsVisibleTo("VanAn.CoreHub")]
[assembly: InternalsVisibleTo("VanAn.Gateway")]
[assembly: InternalsVisibleTo("VanAn.ShopERP")]
[assembly: InternalsVisibleTo("VanAn.KhachLink")]
