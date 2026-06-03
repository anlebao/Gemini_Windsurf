import { IndexedDBService, IndexedDBServiceImpl } from './IndexedDBService';

export interface OfflineOrderService {
    createOrder(order: Order): Promise<void>;
    getOrders(): Promise<Order[]>;
    syncOrders(): Promise<SyncResult>;
    getOrder(orderId: Guid): Promise<Order | null>;
    deleteOrder(orderId: Guid): Promise<void>;
}

export class OfflineOrderServiceImpl implements OfflineOrderService {
    private indexedDB: IndexedDBService;
    private isOnline: boolean = navigator.onLine;
    
    constructor() {
        this.indexedDB = new IndexedDBServiceImpl();
        
        // Monitor online/offline status
        window.addEventListener('online', () => {
            this.isOnline = true;
            this.autoSync();
        });
        
        window.addEventListener('offline', () => {
            this.isOnline = false;
        });
    }
    
    async createOrder(order: Order): Promise<void> {
        try {
            // Initialize IndexedDB if needed
            await this.indexedDB.init();
            
            // Set order status to pending
            order.status = 'pending';
            order.createdAt = new Date().toISOString();
            
            // Store order locally
            await this.indexedDB.storeOrder(order);
            
            // Create sync item
            const syncItem: SyncItem = {
                id: this.generateGuid(),
                orderId: order.id,
                type: 'create',
                status: 'pending',
                data: order,
                createdAt: new Date().toISOString()
            };
            
            await this.indexedDB.storeSyncItem(syncItem);
            
            // Try to sync immediately if online
            if (this.isOnline) {
                await this.syncOrders();
            }
        } catch (error) {
            console.error('Error creating offline order:', error);
            throw error;
        }
    }
    
    async getOrders(): Promise<Order[]> {
        try {
            await this.indexedDB.init();
            return await this.indexedDB.getOrders();
        } catch (error) {
            console.error('Error getting offline orders:', error);
            return [];
        }
    }
    
    async getOrder(orderId: Guid): Promise<Order | null> {
        try {
            await this.indexedDB.init();
            return await this.indexedDB.getOrder(orderId);
        } catch (error) {
            console.error('Error getting offline order:', error);
            return null;
        }
    }
    
    async deleteOrder(orderId: Guid): Promise<void> {
        try {
            await this.indexedDB.init();
            
            // Create sync item for deletion
            const syncItem: SyncItem = {
                id: this.generateGuid(),
                orderId: orderId,
                type: 'delete',
                status: 'pending',
                data: { orderId },
                createdAt: new Date().toISOString()
            };
            
            await this.indexedDB.storeSyncItem(syncItem);
            
            // Try to sync immediately if online
            if (this.isOnline) {
                await this.syncOrders();
            }
        } catch (error) {
            console.error('Error deleting offline order:', error);
            throw error;
        }
    }
    
    async syncOrders(): Promise<SyncResult> {
        const result: SyncResult = {
            success: false,
            syncedCount: 0,
            errorCount: 0,
            errors: []
        };
        
        if (!this.isOnline) {
            result.errors.push('Device is offline');
            return result;
        }
        
        try {
            await this.indexedDB.init();
            const pendingItems = await this.indexedDB.getPendingSync();
            
            for (const item of pendingItems) {
                try {
                    if (item.type === 'create') {
                        await this.syncCreateOrder(item);
                    } else if (item.type === 'delete') {
                        await this.syncDeleteOrder(item);
                    }
                    
                    // Mark as synced
                    await this.indexedDB.markSynced(item.orderId);
                    result.syncedCount++;
                } catch (error) {
                    result.errorCount++;
                    result.errors.push(`Failed to sync order ${item.orderId}: ${error}`);
                }
            }
            
            result.success = result.errorCount === 0;
        } catch (error) {
            result.errors.push(`Sync failed: ${error}`);
        }
        
        return result;
    }
    
    private async syncCreateOrder(syncItem: SyncItem): Promise<void> {
        const order = syncItem.data as Order;
        
        // Call API to create order
        const response = await fetch('/api/orders', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(order)
        });
        
        if (!response.ok) {
            throw new Error(`API call failed: ${response.statusText}`);
        }
        
        // Update local order with server response
        const serverOrder = await response.json();
        await this.indexedDB.storeOrder(serverOrder);
    }
    
    private async syncDeleteOrder(syncItem: SyncItem): Promise<void> {
        const orderId = syncItem.data.orderId;
        
        // Call API to delete order
        const response = await fetch(`/api/orders/${orderId}`, {
            method: 'DELETE'
        });
        
        if (!response.ok) {
            throw new Error(`API call failed: ${response.statusText}`);
        }
        
        // Remove from local storage
        await this.indexedDB.markSynced(orderId);
    }
    
    private async autoSync(): Promise<void> {
        try {
            await this.syncOrders();
        } catch (error) {
            console.error('Auto sync failed:', error);
        }
    }
    
    private generateGuid(): Guid {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        }) as Guid;
    }
}

export interface SyncResult {
    success: boolean;
    syncedCount: number;
    errorCount: number;
    errors: string[];
}

export interface SyncItem {
    id: Guid;
    orderId: Guid;
    type: 'create' | 'update' | 'delete';
    status: 'pending' | 'synced' | 'error';
    data: any;
    createdAt: string;
    syncedAt?: string;
    error?: string;
}
