# E-Invoice Multi-Provider Integration - UI Layout Design

## 📋 Table of Contents
1. [Overview](#overview)
2. [Screen Layouts](#screen-layouts)
3. [Component Library](#component-library)
4. [Responsive Design](#responsive-design)
5. [User Flows](#user-flows)
6. [Implementation Guide](#implementation-guide)

---

## 🎯 Overview

### **Purpose**
This document provides comprehensive UI layout designs for the E-Invoice Multi-Provider Integration system, ensuring an intuitive and efficient user experience for managing POS and E-Invoice provider connections.

### **Target Users**
- **System Administrators**: Provider configuration and monitoring
- **Account Managers**: Invoice management and compliance
- **Support Staff**: Troubleshooting and alert management
- **Business Owners**: Analytics and reporting

---

## 📱 Screen Layouts

### **🏠 Dashboard - Provider Integration Hub**

```
┌─────────────────────────────────────────────────────────────┐
│  📊 VAN AN E-INVOICE INTEGRATION DASHBOARD                   │
├─────────────────────────────────────────────────────────────┤
│  🏢 Shop: ABC Coffee Shop    |  📅 03/05/2026  |  👤 Admin │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐          │
│  │   📈 STATUS │  │  🔌 PROVIDERS│  │  ⚡ HEALTH  │          │
│  │             │  │             │  │             │          │
│  │ ✅ Active: 2│  │ POS: 2/3    │  │ 🟢 Up: 4/5  │          │
│  │ ⚠️ Pending: 1│  │ EInvoice: 3/4│  │ 🟡 Slow: 1  │          │
│  │ ❌ Failed: 0 │  │ Total: 7    │  │ 🔴 Down: 0  │          │
│  └─────────────┘  └─────────────┘  └─────────────┘          │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  📊 TODAY'S ACTIVITY                                   │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │  │
│  │  │ 🧾 Invoices  │  │ 🔄 Sync     │  │ ⚠️ Errors   │  │  │
│  │  │    156      │  │   98.5%     │  │     2       │  │  │
│  │  │   +12%      │  │   +2.1%     │  │   -85%      │  │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  🚨 RECENT ACTIVITY                                    │  │
│  │  • 14:32 - KiotViet sync completed (45 invoices)      │  │
│  │  • 14:25 - Viettel invoice #12345 approved           │  │
│  │  • 14:15 - Sapo connection timeout - retrying...     │  │
│  │  • 14:00 - Daily tax report submitted successfully   │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**Key Components:**
- **Status Cards**: Real-time provider status
- **Activity Metrics**: Daily performance indicators
- **Activity Feed**: Recent system events
- **Navigation Bar**: Quick access to all sections

---

### **📋 Provider Management Screen**

```
┌─────────────────────────────────────────────────────────────┐
│  🔌 PROVIDER MANAGEMENT                                     │
├─────────────────────────────────────────────────────────────┤
│  [🔄 Refresh] [➕ Add Provider] [⚙️ Settings] [📊 Reports]   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  📱 POS PROVIDERS                         💳 E-INVOICE PROVIDERS │
│  ┌─────────────────────────┐         ┌─────────────────────────┐│
│  │ 🟢 KiotViet              │         │ 🟢 Viettel               ││
│  │ Status: Connected       │         │ Status: Connected        ││
│  │ Last Sync: 2 min ago    │         │ Last Submit: 5 min ago   ││
│  │ Invoices Today: 45     │         │ Success Rate: 99.2%      ││
│  │ [⚙️ Configure] [🔄 Sync] │         │ [⚙️ Configure] [📊 Stats]  ││
│  └─────────────────────────┘         └─────────────────────────┘│
│                                                             │
│  🟡 Sapo                    🟡 MISA                          │
│  Status: Slow               Status: Pending                  │
│  Last Sync: 15 min ago     Last Submit: 1 hour ago          │
│  Invoices Today: 23        Success Rate: 97.8%              │
│  [⚙️ Configure] [🔄 Sync]   [⚙️ Configure] [📊 Stats]         ││
│                                                             │
│  🔴 iPOS                    🔴 BKAV                          │
│  Status: Disconnected      Status: Error                    │
│  Last Sync: 2 hours ago    Last Submit: 3 hours ago         │
│  Invoices Today: 0         Success Rate: 0%                 │
│  [🚀 Connect] [🔄 Sync]    [🔧 Fix] [📊 Stats]              ││
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Key Features:**
- **Provider Cards**: Visual status indicators
- **Quick Actions**: Configure, sync, fix issues
- **Performance Metrics**: Real-time statistics
- **Status Indicators**: Color-coded health status

---

### **⚙️ Provider Configuration Screen**

```
┌─────────────────────────────────────────────────────────────┐
│  ⚙️ KIOTVIET PROVIDER CONFIGURATION                         │
├─────────────────────────────────────────────────────────────┤
│  [💾 Save] [🧪 Test Connection] [🔄 Reset] [❌ Cancel]       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  📋 BASIC INFORMATION                                       │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Provider Name: KiotViet                               │  │
│  │ Display Name: KiotViet POS System                    │  │
│  │ Version: v2.1.0                                      │  │
│  │ Status: 🟢 Active                                     │  │
│  │ Priority: 1 (Primary)                                 │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  🔐 AUTHENTICATION                                         │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ API Key: [•••••••••••••••••••••••••••••••••] [👁️] │  │
│  │ Retailer ID: [12345]                                 │  │
│  │ Secret Key: [•••••••••••••••••••••••••••••••••] [👁️] │  │
│  │ Environment: ☑️ Production ☐ Sandbox                 │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  ⚡ CAPABILITIES & LIMITS                                 │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ ☑️ Invoice Creation    ☑️ Invoice Query             │  │
│  │ ☑️ Customer Data       ☐ Batch Processing           │  │
│  │ ☑️ Real-time Sync      ☑️ Webhook Support           │  │
│  │                                                     │  │
│  │ Rate Limit: 100 requests/minute                     │  │
│  │ Timeout: 30 seconds                                │  │
│  │ Max Batch Size: 50 invoices                         │  │
│  │ Retry Count: 3 times                               │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  🔄 SYNC SETTINGS                                         │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Auto Sync: ☑️ Enabled                               │  │
│  │ Sync Interval: [5] minutes                          │  │
│  │ Sync From: [01/01/2024]                             │  │
│  │ Last Sync: 03/05/2026 14:32:45                      │  │
│  │ Next Sync: 03/05/2026 14:37:45                      │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  📊 PERFORMANCE MONITORING                               │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Average Response Time: 1.2s                         │  │
│  │ Success Rate: 99.2%                                │  │
│  │ Error Rate: 0.8%                                   │  │
│  │ Last 24h: 1,234 requests                           │  │
│  │ Uptime: 99.9%                                      │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**Configuration Sections:**
- **Basic Information**: Provider details and status
- **Authentication**: API credentials and security
- **Capabilities**: Supported features and limits
- **Sync Settings**: Automated synchronization options
- **Performance Monitoring**: Real-time metrics

---

### **📊 Health Monitoring Screen**

```
┌─────────────────────────────────────────────────────────────┐
│  📊 PROVIDER HEALTH MONITORING                              │
├─────────────────────────────────────────────────────────────┤
│  [🔄 Refresh] [📈 Analytics] [⚠️ Alerts] [📥 Export]        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  🏥 OVERALL HEALTH STATUS                                   │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ 🟢 HEALTHY: 4 providers                            │  │
│  │ 🟡 WARNING: 2 providers                            │  │
│  │ 🔴 CRITICAL: 1 provider                            │  │
│  │ Overall Score: 87.5%                              │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  📈 PERFORMANCE METRICS (Last 24 Hours)                   │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Total Requests: 15,234                            │  │
│  │ Success Rate: 96.8%                               │  │
│  │ Average Response Time: 1.8s                       │  │
│  │ Error Rate: 3.2%                                  │  │
│  │ Timeout Rate: 0.5%                                │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  📊 PROVIDER PERFORMANCE BREAKDOWN                        │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Provider    │ Status │ Requests │ Success │ Avg Time │  │
│  │─────────────│────────│──────────│─────────│──────────│  │
│  │ KiotViet    │ 🟢     │ 5,234    │ 99.2%   │ 1.2s     │  │
│  │ Viettel     │ 🟢     │ 4,567    │ 98.8%   │ 2.1s     │  │
│  │ Sapo        │ 🟡     │ 3,456    │ 94.5%   │ 3.2s     │  │
│  │ MISA        │ 🟡     │ 1,234    │ 92.1%   │ 2.8s     │  │
│  │ BKAV        │ 🔴     │   743    │ 78.9%   │ 5.6s     │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  ⚠️ RECENT ERRORS & INCIDENTS                               │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ 14:25 - BKAV: Connection timeout (5.6s)             │  │
│  │ 14:20 - Sapo: Rate limit exceeded (100 req/min)     │  │
│  │ 14:15 - MISA: Invalid API response format           │  │
│  │ 14:10 - Viettel: Invoice submission failed          │  │
│  │ 14:05 - KiotViet: Sync completed successfully       │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**Monitoring Features:**
- **Health Overview**: System-wide status summary
- **Performance Metrics**: Detailed statistics
- **Provider Breakdown**: Individual provider performance
- **Error Tracking**: Recent incidents and issues

---

### **🧾 Invoice Management Screen**

```
┌─────────────────────────────────────────────────────────────┐
│  🧾 INVOICE MANAGEMENT                                      │
├─────────────────────────────────────────────────────────────┤
│  🔍 [Search...] 📅 [Date Filter] 📊 [Status Filter] 🔄 Refresh │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  📊 INVOICE SUMMARY                                        │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Total Today: 156  |  Pending: 12  |  Approved: 144  │  │
│  │ This Week: 892   |  Failed: 2    |  Processing: 8  │  │
│  │ This Month: 3,456| Revenue: ₫45.2M                 │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  📋 INVOICE LIST                                           │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ ID     │ Customer      │ Amount   │ Status   │ Provider│  │
│  │─────────────│─────────────│──────────│─────────│────────│  │
│  │ #12345 │ Nguyễn Văn A │ ₫250,000 │ 🟢 Appr │ Viettel │  │
│  │ #12344 │ Trần Thị B  │ ₫180,000 │ 🟡 Pend │ KiotViet│  │
│  │ #12343 │ Lê Văn C    │ ₫320,000 │ 🟢 Appr │ Viettel │  │
│  │ #12342 │ Phạm Thị D  │ ₫150,000 │ 🔴 Fail │ MISA    │  │
│  │ #12341 │ Hoàng Văn E │ ₫280,000 │ 🟡 Proc │ Sapo    │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  📝 ACTIONS: [📄 Create Invoice] [📥 Import] [📤 Export] [🔄 Sync All] │
└─────────────────────────────────────────────────────────────┘
```

**Invoice Management Features:**
- **Summary Cards**: Quick statistics overview
- **Search & Filter**: Find specific invoices
- **Status Tracking**: Monitor invoice lifecycle
- **Bulk Actions**: Process multiple invoices

---

### **⚠️ Alert Management Screen**

```
┌─────────────────────────────────────────────────────────────┐
│  ⚠️ ALERT MANAGEMENT                                        │
├─────────────────────────────────────────────────────────────┤
│  [🔕 Mark All Read] [⚙️ Alert Settings] [📥 Export] [🔄 Refresh] │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  🔔 ACTIVE ALERTS (3)                                      │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ 🔴 CRITICAL: BKAV provider down for 15 minutes       │  │
│  │ Time: 14:25 | Duration: 15 min                       │  │
│  │ [🔧 Fix Now] [📞 Contact Support] [🔕 Dismiss]       │  │
│  │─────────────────────────────────────────────────────│  │
│  │ 🟡 WARNING: Sapo response time > 5 seconds           │  │
│  │ Time: 14:20 | Duration: 8 min                        │  │
│  │ [📊 View Details] [🔄 Restart] [🔕 Dismiss]          │  │
│  │─────────────────────────────────────────────────────│  │
│  │ 🟡 INFO: Viettel rate limit reached (80%)            │  │
│  │ Time: 14:15 | Duration: 5 min                        │  │
│  │ [⏱️ Wait] [🔄 Retry] [🔕 Dismiss]                    │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  📋 ALERT HISTORY                                          │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ 13:45 - ✅ RESOLVED: KiotViet sync completed        │  │
│  │ 13:30 - ✅ RESOLVED: MISA API key updated           │  │
│  │ 13:15 - 🔴 CRITICAL: Database connection lost      │  │
│  │ 13:00 - ✅ RESOLVED: Database connection restored  │  │
│  │ 12:45 - 🟡 WARNING: High memory usage detected      │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  ⚙️ ALERT CONFIGURATION                                    │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ ☑️ Provider downtime alerts                           │  │
│  │ ☑️ Performance degradation alerts                    │  │
│  │ ☑️ Rate limit warnings                               │  │
│  │ ☑️ Invoice submission failures                       │  │
│  │ ☑️ Daily compliance reports                           │  │
│  │ Notification: ☑️ Email ☑️ SMS ☐ Slack                │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**Alert Management Features:**
- **Active Alerts**: Real-time notifications
- **Alert History**: Past incidents tracking
- **Configuration**: Customize alert settings
- **Quick Actions**: Resolve issues directly

---

### **📈 Analytics & Reports Screen**

```
┌─────────────────────────────────────────────────────────────┐
│  📈 ANALYTICS & REPORTS                                     │
├─────────────────────────────────────────────────────────────┤
│  📅 [This Week] [This Month] [Custom Range] [📥 Export]     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  📊 KEY METRICS                                            │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Total Invoices: 3,456    Revenue: ₫45.2M           │  │
│  │ Success Rate: 96.8%       Avg Processing: 1.8s     │  │
│  │ Active Providers: 6       Compliance: 100%         │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  📈 PERFORMANCE CHARTS                                      │
│  ┌─────────────────────┐  ┌─────────────────────┐          │
│  │ 📊 Daily Volume      │  │ ⚡ Response Time    │          │
│  │                     │  │                     │          │
│  │   ████             │  │      ████           │          │
│  │  ██████            │  │     ██████          │          │
│  │ ████████           │  │    ████████         │          │
│  │████████████████     │  │████████████████      │          │
│  │ Mon Tue Wed Thu Fri │  │ Mon Tue Wed Thu Fri │          │
│  └─────────────────────┘  └─────────────────────┘          │
│                                                             │
│  🎯 PROVIDER PERFORMANCE                                   │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Provider    │ Invoices │ Success │ Revenue  │ Share   │  │
│  │─────────────│──────────│─────────│──────────│─────────│  │
│  │ KiotViet    │ 1,234    │ 99.2%   │ ₫12.3M  │ 27.2%   │  │
│  │ Viettel     │ 1,567    │ 98.8%   │ ₫18.9M  │ 41.8%   │  │
│  │ Sapo        │   456    │ 94.5%   │ ₫5.4M   │ 11.9%   │  │
│  │ MISA        │   199    │ 92.1%   │ ₫2.8M   │ 6.2%    │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  📋 COMPLIANCE REPORT                                       │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ ✅ TT152-2025 Compliance: 100%                        │  │
│  │ ✅ Digital Signatures: 100% Valid                    │  │
│  │ ✅ Tax Authority Submission: 100% Acknowledged        │  │
│  │ ✅ Audit Trail: Complete (5-year retention)          │  │
│  │ ✅ Idempotency: 0 duplicate invoices                 │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**Analytics Features:**
- **Key Metrics**: Performance indicators
- **Interactive Charts**: Visual data representation
- **Provider Comparison**: Performance analysis
- **Compliance Reports**: Legal compliance tracking

---

## 🎨 Component Library

### **🎨 Color Scheme**
```css
:root {
  /* Primary Colors */
  --primary-blue: #2563eb;
  --primary-green: #16a34a;
  --primary-orange: #ea580c;
  
  /* Status Colors */
  --success-green: #22c55e;
  --warning-yellow: #eab308;
  --error-red: #ef4444;
  --info-blue: #3b82f6;
  
  /* Neutral Colors */
  --gray-50: #f9fafb;
  --gray-100: #f3f4f6;
  --gray-500: #6b7280;
  --gray-900: #111827;
}
```

### **📐 Typography**
```css
.text-xs { font-size: 0.75rem; line-height: 1rem; }
.text-sm { font-size: 0.875rem; line-height: 1.25rem; }
.text-base { font-size: 1rem; line-height: 1.5rem; }
.text-lg { font-size: 1.125rem; line-height: 1.75rem; }
.text-xl { font-size: 1.25rem; line-height: 1.75rem; }
.text-2xl { font-size: 1.5rem; line-height: 2rem; }
.text-3xl { font-size: 1.875rem; line-height: 2.25rem; }
```

### **🔧 Button Components**
```html
<!-- Primary Button -->
<button class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg">
  Save Changes
</button>

<!-- Secondary Button -->
<button class="bg-gray-200 hover:bg-gray-300 text-gray-900 px-4 py-2 rounded-lg">
  Cancel
</button>

<!-- Success Button -->
<button class="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-lg">
  Approve
</button>

<!-- Danger Button -->
<button class="bg-red-600 hover:bg-red-700 text-white px-4 py-2 rounded-lg">
  Delete
</button>
```

### **📊 Card Components**
```html
<!-- Status Card -->
<div class="bg-white rounded-lg shadow p-6">
  <div class="flex items-center justify-between">
    <div>
      <p class="text-sm text-gray-600">Active Providers</p>
      <p class="text-2xl font-bold text-gray-900">5/7</p>
    </div>
    <div class="bg-green-100 rounded-full p-3">
      <svg class="w-6 h-6 text-green-600" fill="none" stroke="currentColor">
        <!-- Icon SVG -->
      </svg>
    </div>
  </div>
</div>

<!-- Provider Card -->
<div class="bg-white rounded-lg shadow p-6 border-l-4 border-green-500">
  <div class="flex items-center justify-between mb-4">
    <h3 class="text-lg font-semibold">KiotViet</h3>
    <span class="bg-green-100 text-green-800 text-xs px-2 py-1 rounded">Connected</span>
  </div>
  <div class="space-y-2">
    <div class="flex justify-between text-sm">
      <span class="text-gray-600">Last Sync:</span>
      <span>2 minutes ago</span>
    </div>
    <div class="flex justify-between text-sm">
      <span class="text-gray-600">Invoices Today:</span>
      <span class="font-semibold">45</span>
    </div>
  </div>
  <div class="mt-4 flex space-x-2">
    <button class="bg-blue-600 text-white px-3 py-1 rounded text-sm">Configure</button>
    <button class="bg-gray-200 text-gray-700 px-3 py-1 rounded text-sm">Sync</button>
  </div>
</div>
```

### **📋 Table Components**
```html
<!-- Data Table -->
<div class="bg-white shadow rounded-lg overflow-hidden">
  <table class="min-w-full divide-y divide-gray-200">
    <thead class="bg-gray-50">
      <tr>
        <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
          Provider
        </th>
        <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
          Status
        </th>
        <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
          Requests
        </th>
        <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
          Success Rate
        </th>
        <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
          Avg Time
        </th>
      </tr>
    </thead>
    <tbody class="bg-white divide-y divide-gray-200">
      <tr>
        <td class="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">
          KiotViet
        </td>
        <td class="px-6 py-4 whitespace-nowrap">
          <span class="bg-green-100 text-green-800 text-xs px-2 py-1 rounded">Connected</span>
        </td>
        <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
          5,234
        </td>
        <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
          99.2%
        </td>
        <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
          1.2s
        </td>
      </tr>
    </tbody>
  </table>
</div>
```

---

## 📱 Responsive Design

### **📱 Mobile Layout (< 768px)**
```
┌─────────────────────┐
│ 📊 Van An E-Invoice │
├─────────────────────┤
│ 🏢 ABC Coffee Shop  │
│ 👤 Admin           │
├─────────────────────┤
│ ┌─────┐ ┌─────┐    │
│ │ 📈  │ │ 🔌  │    │
│ │ 2   │ │ 5/7 │    │
│ │ ⚠️1 │ │ 🟢4 │    │
│ └─────┘ └─────┘    │
├─────────────────────┤
│ 📊 Today's Activity │
│ 🧾 156 Invoices     │
│ 🔄 98.5% Sync       │
│ ⚠️ 2 Errors         │
├─────────────────────┤
│ 🚨 Recent Activity  │
│ • KiotViet sync OK  │
│ • Viettel #12345    │
│ • Sapo timeout...   │
└─────────────────────┘
```

### **📱 Tablet Layout (768px - 1024px)**
```
┌─────────────────────────────────┐
│ 📊 Van An E-Invoice Dashboard  │
├─────────────────────────────────┤
│ 🏢 Shop: ABC Coffee Shop        │
│ 📅 03/05/2026 | 👤 Admin      │
├─────────────────────────────────┤
│ ┌─────────────┐ ┌─────────────┐ │
│ │ 📈 STATUS   │ │ 🔌 PROVIDERS│ │
│ │ ✅ Active:2 │ │ POS: 2/3    │ │
│ │ ⚠️ Pending:1 │ │ EInv: 3/4   │ │
│ │ ❌ Failed:0  │ │ Total: 7    │ │
│ └─────────────┘ └─────────────┘ │
│ ┌─────────────┐ ┌─────────────┐ │
│ │ ⚡ HEALTH   │ │ 📊 ACTIVITY │ │
│ │ 🟢 Up: 4/5  │ │ 🧾 156      │ │
│ │ 🟡 Slow: 1  │ │ 🔄 98.5%   │ │
│ │ 🔴 Down: 0  │ │ ⚠️ 2       │ │
│ └─────────────┘ └─────────────┘ │
├─────────────────────────────────┤
│ 🚨 Recent Activity             │
│ • 14:32 - KiotViet sync OK     │
│ • 14:25 - Viettel #12345       │
│ • 14:15 - Sapo timeout...     │
└─────────────────────────────────┘
```

### **💻 Desktop Layout (> 1024px)**
- Full dashboard with all components visible
- Side navigation for quick access
- Detailed metrics and charts
- Multi-panel layout for complex tasks

---

## 🔄 User Flows

### **🚀 Onboarding Flow**
1. **Welcome Screen** → Introduction to E-Invoice integration
2. **Provider Selection** → Choose POS and E-Invoice providers
3. **API Configuration** → Enter API keys and settings
4. **Connection Test** → Validate provider connections
5. **Sync Setup** → Configure sync schedules
6. **Go Live** → Start using the system

### **⚙️ Configuration Flow**
1. **Provider List** → Select provider to configure
2. **Authentication** → Enter API credentials
3. **Capabilities** → Configure supported features
4. **Sync Settings** → Set up automated sync
5. **Testing** → Test connection and functionality
6. **Activation** → Enable provider

### **📊 Monitoring Flow**
1. **Dashboard Overview** → Check system health
2. **Provider Status** → Review individual providers
3. **Performance Metrics** → Analyze detailed statistics
4. **Alert Management** → Handle active issues
5. **Report Generation** → Create compliance reports

---

## 🛠️ Implementation Guide

### **📂 File Structure**
```
5_WebApps/KhachLink/
├── Pages/
│   ├── EInvoice/
│   │   ├── Dashboard.cshtml
│   │   ├── ProviderManagement.cshtml
│   │   ├── ProviderConfiguration.cshtml
│   │   ├── HealthMonitoring.cshtml
│   │   ├── InvoiceManagement.cshtml
│   │   ├── AlertManagement.cshtml
│   │   └── Analytics.cshtml
│   └── Shared/
│       ├── Components/
│       │   ├── ProviderCard.cshtml
│       │   ├── StatusCard.cshtml
│       │   ├── MetricsCard.cshtml
│       │   └── AlertCard.cshtml
│       └── Layouts/
│           └── _EInvoiceLayout.cshtml
├── wwwroot/
│   ├── css/
│   │   ├── einvoice.css
│   │   └── components.css
│   ├── js/
│   │   ├── einvoice.js
│   │   ├── providers.js
│   │   └── dashboard.js
│   └── images/
│       ├── providers/
│       └── icons/
```

### **🎯 CSS Framework**
```css
/* einvoice.css */
.einvoice-dashboard {
  @apply bg-gray-50 min-h-screen;
}

.provider-card {
  @apply bg-white rounded-lg shadow-md p-6 border-l-4;
}

.provider-card.connected {
  @apply border-green-500;
}

.provider-card.warning {
  @apply border-yellow-500;
}

.provider-card.error {
  @apply border-red-500;
}

.status-indicator {
  @apply inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium;
}

.status-indicator.success {
  @apply bg-green-100 text-green-800;
}

.status-indicator.warning {
  @apply bg-yellow-100 text-yellow-800;
}

.status-indicator.error {
  @apply bg-red-100 text-red-800;
}
```

### **⚡ JavaScript Components**
```javascript
// providers.js
class ProviderManager {
  constructor() {
    this.providers = [];
    this.init();
  }

  async init() {
    await this.loadProviders();
    this.setupEventListeners();
    this.startRealTimeUpdates();
  }

  async loadProviders() {
    const response = await fetch('/api/providers');
    this.providers = await response.json();
    this.renderProviderCards();
  }

  renderProviderCards() {
    const container = document.getElementById('provider-cards');
    container.innerHTML = this.providers.map(provider => 
      this.createProviderCard(provider)
    ).join('');
  }

  createProviderCard(provider) {
    return `
      <div class="provider-card ${provider.status}">
        <div class="flex justify-between items-start">
          <div>
            <h3 class="text-lg font-semibold">${provider.name}</h3>
            <p class="text-sm text-gray-600">${provider.type}</p>
          </div>
          <span class="status-indicator ${provider.status}">
            ${provider.status}
          </span>
        </div>
        <div class="mt-4 space-y-2">
          <div class="flex justify-between text-sm">
            <span>Last Sync:</span>
            <span>${provider.lastSync}</span>
          </div>
          <div class="flex justify-between text-sm">
            <span>Invoices Today:</span>
            <span class="font-semibold">${provider.invoicesToday}</span>
          </div>
        </div>
        <div class="mt-4 flex space-x-2">
          <button onclick="configureProvider('${provider.id}')" 
                  class="bg-blue-600 text-white px-3 py-1 rounded text-sm">
            Configure
          </button>
          <button onclick="syncProvider('${provider.id}')" 
                  class="bg-gray-200 text-gray-700 px-3 py-1 rounded text-sm">
            Sync
          </button>
        </div>
      </div>
    `;
  }

  async syncProvider(providerId) {
    try {
      const response = await fetch(`/api/providers/${providerId}/sync`, {
        method: 'POST'
      });
      
      if (response.ok) {
        this.showNotification('Sync started successfully', 'success');
        await this.loadProviders(); // Refresh data
      } else {
        this.showNotification('Sync failed', 'error');
      }
    } catch (error) {
      this.showNotification('Sync error: ' + error.message, 'error');
    }
  }

  showNotification(message, type) {
    // Implement notification system
    console.log(`${type}: ${message}`);
  }

  setupEventListeners() {
    // Setup event listeners for real-time updates
  }

  startRealTimeUpdates() {
    // Start WebSocket or polling for real-time updates
    setInterval(() => this.loadProviders(), 30000); // Update every 30 seconds
  }
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
  new ProviderManager();
});
```

### **🔧 Razor Page Examples**
```html
<!-- Pages/EInvoice/Dashboard.cshtml -->
@page
@model VanAn.WebApp.Models.EInvoiceDashboardModel
@{
    Layout = "_EInvoiceLayout";
    ViewData["Title"] = "E-Invoice Dashboard";
}

<div class="einvoice-dashboard">
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-8">
        <!-- Status Cards -->
        <partial name="_StatusCard" model="@Model.StatusData" />
        <partial name="_ProviderCard" model="@Model.ProviderData" />
        <partial name="_HealthCard" model="@Model.HealthData" />
    </div>

    <!-- Activity Section -->
    <div class="bg-white rounded-lg shadow p-6 mb-8">
        <h2 class="text-xl font-semibold mb-4">Today's Activity</h2>
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <partial name="_ActivityCard" model="@Model.InvoiceActivity" />
            <partial name="_ActivityCard" model="@Model.SyncActivity" />
            <partial name="_ActivityCard" model="@Model.ErrorActivity" />
        </div>
    </div>

    <!-- Recent Activity -->
    <div class="bg-white rounded-lg shadow p-6">
        <h2 class="text-xl font-semibold mb-4">Recent Activity</h2>
        <div class="space-y-3">
            @foreach (var activity in Model.RecentActivities)
            {
                <div class="flex items-center space-x-3 text-sm">
                    <span class="text-gray-500">@activity.Time.ToString("HH:mm")</span>
                    <span>@activity.Message</span>
                </div>
            }
        </div>
    </div>
</div>
```

### **📊 API Integration**
```csharp
// Controllers/EInvoiceController.cs
[ApiController]
[Route("api/[controller]")]
public class EInvoiceController : ControllerBase
{
    [HttpGet("providers")]
    public async Task<ActionResult<List<ProviderDto>>> GetProviders()
    {
        var providers = await _providerService.GetAllProvidersAsync();
        return Ok(providers);
    }

    [HttpPost("providers/{providerId}/sync")]
    public async Task<ActionResult> SyncProvider(string providerId)
    {
        await _providerService.SyncProviderAsync(providerId);
        return Ok();
    }

    [HttpGet("dashboard/status")]
    public async Task<ActionResult<DashboardStatusDto>> GetDashboardStatus()
    {
        var status = await _dashboardService.GetStatusAsync();
        return Ok(status);
    }
}
```

---

## 🎯 Implementation Checklist

### **✅ Frontend Components**
- [ ] Dashboard layout and components
- [ ] Provider management interface
- [ ] Configuration forms
- [ ] Health monitoring dashboard
- [ ] Invoice management system
- [ ] Alert management interface
- [ ] Analytics and reports

### **✅ Responsive Design**
- [ ] Mobile layout optimization
- [ ] Tablet layout adaptation
- [ ] Desktop layout enhancement
- [ ] Touch-friendly interactions
- [ ] Accessibility compliance

### **✅ Real-time Features**
- [ ] WebSocket integration
- [ ] Live status updates
- [ ] Real-time notifications
- [ ] Auto-refresh functionality
- [ ] Progress indicators

### **✅ User Experience**
- [ ] Intuitive navigation
- [ ] Clear visual hierarchy
- [ ] Consistent design language
- [ ] Error handling and feedback
- [ ] Loading states and skeletons

---

## 📚 References

### **🎨 Design Resources**
- **Layout Reference**: This document
- **Component Library**: Tailwind CSS + Custom Components
- **Icon Library**: Heroicons or Lucide Icons
- **Color Palette**: Van An Brand Guidelines

### **🔧 Technical References**
- **Backend Integration**: E-Invoice Multi-Provider Integration Design
- **API Documentation**: Provider API Specifications
- **Database Schema**: Provider Configuration Schema
- **Security Guidelines**: Authentication & Authorization

---

*Last Updated: May 3, 2026*
*Version: 1.0*
*Status: Ready for Implementation*
