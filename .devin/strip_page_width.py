#!/usr/bin/env python3
# Batch 2 follow-up (user feedback): widen KhachLink display area on ALL pages.
# Remove per-page container inline max-width caps (500/600/700/900px) so pages use the
# global .khachlink-main max-width (1200px) + Bootstrap .container responsive widths.
# Only touches the page-container div pattern — intentional inner caps (hero text,
# search box, banners, Qr/Wallet component) are left untouched.
import re
import io

BASE = "5_WebApps/KhachLink"

FILES = [
    "Pages/SalesmanStore.razor",
    "Pages/DeliveryTracking.razor",
    "Pages/OrderTracking.razor",
    "Pages/NearbyProducts.razor",
    "Pages/NearbyOrders.razor",
    "Pages/Wallet.razor",
    "Pages/SalesDashboard.razor",
    "Pages/OrderHistory.razor",
    "Pages/AllianceWallet.razor",
    "Pages/ActiveDeliveries.razor",
    "Pages/LoyaltyCard.razor",
    "Pages/Missions.razor",
    "Pages/RedemptionCatalog.razor",
    "Pages/Register.razor",
    "Pages/Claim.razor",
    "Pages/Profile.razor",
    "Pages/SalesmanQR.razor",
    "Pages/CollaboratorVerification.razor",
    "Pages/ProductSearchResults.razor",
    "Pages/Campaigns.razor",
    "Pages/Campaign.razor",
]

# container div with inline max-width cap (any 500/600/700/900px)
CONTAINER_RE = re.compile(r'(<div class="container[^"]*") style="max-width:(?:500|600|700|900)px;"')

changed = []
for f in FILES:
    path = f"{BASE}/{f}"
    with io.open(path, "r", encoding="utf-8-sig", newline="") as fh:
        content = fh.read()
    new_content, n = CONTAINER_RE.subn(r"\1", content)
    if n:
        # utf-8-sig keeps the BOM the repo uses for .razor files
        with io.open(path, "w", encoding="utf-8-sig", newline="") as fh:
            fh.write(new_content)
        changed.append(f"{f}: {n} cap(s) removed")

print("\n".join(changed) if changed else "NO CHANGES")
print(f"TOTAL files changed: {len(changed)}")
