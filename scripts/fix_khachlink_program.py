import sys
sys.stdout.reconfigure(encoding="utf-8")

path = "5_WebApps/KhachLink/Program.cs"
with open(path, "r", encoding="utf-8-sig") as f:
    content = f.read()

# Remove NullLoyaltyRewardsRepository stub (already reverted but ensure clean)
# Remove all repository registrations that need IVanAnDbContext
repos_to_remove = [
    # CustomerRepository
    ("            _ = builder.Services.AddScoped<CoreHub.Domain.Repositories.ICustomerRepository, "
     "CoreHub.Infrastructure.Repositories.CustomerRepository>();\n"),
    # OrderRepository
    ("            _ = builder.Services.AddScoped<CoreHub.Repositories.IOrderRepository, "
     "CoreHub.Repositories.OrderRepository>();\n"),
    # SocialCampaignRepository
    ("            _ = builder.Services.AddScoped<CoreHub.Repositories.ISocialCampaignRepository, "
     "CoreHub.Infrastructure.Repositories.SocialCampaignRepository>();\n"),
    # LoyaltyRewardsRepository (NullStub version)
    ("            // KhachLink uses a null (no-DB) stub - actual loyalty data is managed by CoreHub via API\n"
     "            _ = builder.Services.AddScoped<CoreHub.Repositories.ILoyaltyRewardsRepository, "
     "VanAn.KhachLink.Infrastructure.NullLoyaltyRewardsRepository>();\n"),
    # LoyaltyRewardsRepository (original version, just in case)
    ("            _ = builder.Services.AddScoped<CoreHub.Repositories.ILoyaltyRewardsRepository, "
     "CoreHub.Infrastructure.Repositories.LoyaltyRewardsRepository>();\n"),
    # SystemMetricsRepository
    ("            _ = builder.Services.AddScoped<CoreHub.Repositories.ISystemMetricsRepository, "
     "CoreHub.Infrastructure.Repositories.SystemMetricsRepository>();\n"),
    # ILoyaltyRewardsService (needs repository)
    ("            _ = builder.Services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();\n"),
    # ICustomerService (needs ICustomerRepository)
    ("            _ = builder.Services.AddScoped<ICustomerService, CustomerService>();\n"),
]

changes = 0
for pattern in repos_to_remove:
    if pattern in content:
        content = content.replace(pattern, "")
        print(f"Removed: {pattern.strip()[:80]}")
        changes += 1
    else:
        # try without trailing newline
        alt = pattern.rstrip("\n")
        if alt in content:
            content = content.replace(alt, "")
            print(f"Removed (alt): {alt.strip()[:80]}")
            changes += 1

# Also remove the "Register Repositories" section header if now empty
section_header = "\n\n\n            // Register Repositories\n\n"
if section_header in content:
    content = content.replace(section_header, "\n\n")
    print("Cleaned empty Repositories section header")
    changes += 1

print(f"\nTotal changes: {changes}")

with open(path, "w", encoding="utf-8-sig") as f:
    f.write(content)
print("Saved.")
