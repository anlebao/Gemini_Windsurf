import sys
sys.stdout.reconfigure(encoding="utf-8")

path = "5_WebApps/KhachLink/Program.cs"

with open(path, "r", encoding="utf-8-sig") as f:
    content = f.read()

# Fix 1: Add using statements after existing CoreHub.Services using
old_usings = "using VanAn.CoreHub.Services;"
new_usings = ("using VanAn.CoreHub.Services;\n\n"
              "using VanAn.CoreHub.Infrastructure;\n\n"
              "using Microsoft.EntityFrameworkCore;")

# Fix 2: Register DbContext + IVanAnDbContext after ITenantService
old_di = "            _ = builder.Services.AddScoped<ITenantService, TenantService>();"
new_di = (
    "            _ = builder.Services.AddScoped<ITenantService, TenantService>();\n\n"
    "            // Register VanAnDbContext so repositories (Loyalty, Customer, etc.) can resolve IVanAnDbContext\n"
    "            string khachLinkConnStr = builder.Configuration.GetConnectionString(\"DefaultConnection\")\n"
    "                ?? builder.Configuration[\"ConnectionStrings__DefaultConnection\"]\n"
    "                ?? \"Host=vanan-postgres;Port=5432;Database=vanan_db;Username=vanan_admin;Password=vanan_secure_password_2024\";\n"
    "            _ = builder.Services.AddDbContext<VanAnDbContext>(options =>\n"
    "                options.UseNpgsql(khachLinkConnStr));\n"
    "            _ = builder.Services.AddScoped<IVanAnDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());"
)

changed = False

if old_usings in content:
    content = content.replace(old_usings, new_usings, 1)
    print("usings: OK")
    changed = True
else:
    print("usings: NOT FOUND")

if old_di in content:
    content = content.replace(old_di, new_di, 1)
    print("DI: OK")
    changed = True
else:
    print("DI: NOT FOUND")

if changed:
    with open(path, "w", encoding="utf-8-sig") as f:
        f.write(content)
    print("Saved.")
else:
    print("No changes written.")
