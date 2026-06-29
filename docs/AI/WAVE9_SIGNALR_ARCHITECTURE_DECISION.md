# Wave 9 SignalR Architecture Decision

## Context
Wave 9 goal: Replace SignalR with Short Polling + Web Push for KhachLink's order status updates.

## Investigation Results

### SignalR Hubs in Gateway
- **OrderHub** (`2_Gateway/Hubs/OrderHub.cs`): Used by ShopERP for shop-specific real-time updates
- **KitchenHub** (`2_Gateway/Hubs/KitchenHub.cs`): Used by ShopERP for kitchen display real-time updates

### Usage Analysis
1. **KhachLink OrderTracking.razor**: 
   - ✅ Already migrated to polling (Session 1 implementation)
   - ✅ Uses PeriodicTimer with 5-second interval
   - ✅ Visibility-aware polling (pauses when tab hidden)
   - ❌ No SignalR dependencies

2. **ShopERP Kitchen Display**:
   - ✅ Still uses SignalR for real-time kitchen updates
   - ✅ Different use case (staff dashboard vs customer order tracking)
   - ✅ Requires low-latency updates for kitchen operations

## Decision
**SignalR hubs will NOT be removed.**

### Rationale:
1. **Different Use Cases**:
   - KhachLink: Customer-facing order tracking (polling + push is appropriate)
   - ShopERP: Staff-facing kitchen display (requires real-time updates)

2. **Wave 9 Scope**:
   - Target: KhachLink's order status updates
   - Not in scope: ShopERP's kitchen display functionality

3. **Technical Justification**:
   - Kitchen display requires sub-second updates for operational efficiency
   - Polling would add unacceptable latency for kitchen operations
   - SignalR is appropriate for internal staff dashboards

4. **Architecture Separation**:
   - KhachLink (Customer): Polling (5s) + Push (event-driven)
   - ShopERP (Staff): SignalR (real-time, low-latency)

## Implementation Status
✅ **KhachLink OrderTracking.razor**: Fully migrated to polling (Session 1)
✅ **Push Notification Infrastructure**: Implemented (Session 2)
✅ **Push Subscription Persistence**: Implemented (Session 3)
✅ **NATS Integration**: Implemented (Session 3)
✅ **ShopERP Kitchen Display**: SignalR retained (appropriate use case)

## Conclusion
Wave 9 objectives have been met for KhachLink. SignalR remains in Gateway for ShopERP's legitimate use cases.

---
**Date:** 2026-06-29  
**Wave:** 9 (KhachLink-W4)  
**Status:** Architecture decision documented