import { IndexedDBService, IndexedDBServiceImpl } from './IndexedDBService';

export interface CartService {
    addItem(item: CartItem): Promise<void>;
    removeItem(productId: string): Promise<void>;
    updateQuantity(productId: string, quantity: number): Promise<void>;
    getItems(): Promise<CartItem[]>;
    getTotal(): Promise<number>;
    clearCart(): Promise<void>;
    syncCart(): Promise<SyncResult>;
    saveCartOffline(): Promise<void>;
    loadCartOffline(): Promise<void>;
}

export class EnhancedCartServiceImpl implements CartService {
    private indexedDB: IndexedDBService;
    private isOnline: boolean = navigator.onLine;
    private cartKey = 'user_cart';
    
    constructor() {
        this.indexedDB = new IndexedDBServiceImpl();
        
        // Monitor online/offline status
        window.addEventListener('online', () => {
            this.isOnline = true;
            this.syncCart();
        });
        
        window.addEventListener('offline', () => {
            this.isOnline = false;
        });
    }
    
    async addItem(item: CartItem): Promise<void> {
        try {
            // Get current cart
            const items = await this.getItems();
            const existingItem = items.find(i => i.productId === item.productId);
            
            if (existingItem) {
                // Update quantity
                existingItem.quantity += item.quantity;
                existingItem.totalPrice = existingItem.quantity * existingItem.unitPrice;
            } else {
                // Add new item
                items.push(item);
            }
            
            // Save to IndexedDB
            await this.indexedDB.init();
            await this.indexedDB.storeCart({
                id: this.cartKey,
                items: items,
                updatedAt: new Date().toISOString()
            });
            
            // Try to sync immediately if online
            if (this.isOnline) {
                await this.syncCart();
            }
        } catch (error) {
            console.error('Error adding item to cart:', error);
            throw error;
        }
    }
    
    async removeItem(productId: string): Promise<void> {
        try {
            const items = await this.getItems();
            const item = items.find(i => i.productId === productId);
            
            if (item) {
                items.splice(items.indexOf(item), 1);
                
                await this.indexedDB.init();
                await this.indexedDB.storeCart({
                    id: this.cartKey,
                    items: items,
                    updatedAt: new Date().toISOString()
                });
                
                // Try to sync immediately if online
                if (this.isOnline) {
                    await this.syncCart();
                }
            }
        } catch (error) {
            console.error('Error removing item from cart:', error);
            throw error;
        }
    }
    
    async updateQuantity(productId: string, quantity: number): Promise<void> {
        try {
            if (quantity <= 0) {
                await this.removeItem(productId);
                return;
            }
            
            const items = await this.getItems();
            const item = items.find(i => i.productId === productId);
            
            if (item) {
                item.quantity = quantity;
                item.totalPrice = quantity * item.unitPrice;
                
                await this.indexedDB.init();
                await this.indexedDB.storeCart({
                    id: this.cartKey,
                    items: items,
                    updatedAt: new Date().toISOString()
                });
                
                // Try to sync immediately if online
                if (this.isOnline) {
                    await this.syncCart();
                }
            }
        } catch (error) {
            console.error('Error updating cart item quantity:', error);
            throw error;
        }
    }
    
    async getItems(): Promise<CartItem[]> {
        try {
            await this.indexedDB.init();
            const cart = await this.indexedDB.getCart(this.cartKey);
            return cart?.items || [];
        } catch (error) {
            console.error('Error getting cart items:', error);
            return [];
        }
    }
    
    async getTotal(): Promise<number> {
        try {
            const items = await this.getItems();
            return items.reduce((total, item) => total + item.totalPrice, 0);
        } catch (error) {
            console.error('Error calculating cart total:', error);
            return 0;
        }
    }
    
    async clearCart(): Promise<void> {
        try {
            await this.indexedDB.init();
            await this.indexedDB.storeCart({
                id: this.cartKey,
                items: [],
                updatedAt: new Date().toISOString()
            });
            
            // Try to sync immediately if online
            if (this.isOnline) {
                await this.syncCart();
            }
        } catch (error) {
            console.error('Error clearing cart:', error);
            throw error;
        }
    }
    
    async syncCart(): Promise<SyncResult> {
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
            const cart = await this.indexedDB.getCart(this.cartKey);
            
            if (!cart || !cart.items.length) {
                result.success = true;
                result.errors.push('No items to sync');
                return result;
            }
            
            // Get server cart
            const serverItems = await this.getServerCart();
            
            // Resolve conflicts
            const mergedItems = this.mergeCartItems(cart.items, serverItems);
            
            // Sync merged items to server
            await this.syncToServer(mergedItems);
            
            // Update offline cart with merged items
            await this.indexedDB.storeCart({
                id: this.cartKey,
                items: mergedItems,
                updatedAt: new Date().toISOString()
            });
            
            result.success = true;
            result.syncedCount = mergedItems.length;
        } catch (error) {
            result.errorCount++;
            result.errors.push(`Cart sync failed: ${error}`);
        }
        
        return result;
    }
    
    async saveCartOffline(): Promise<void> {
        try {
            const serverItems = await this.getServerCart();
            
            await this.indexedDB.init();
            await this.indexedDB.storeCart({
                id: this.cartKey,
                items: serverItems,
                updatedAt: new Date().toISOString()
            });
        } catch (error) {
            console.error('Error saving cart offline:', error);
            throw error;
        }
    }
    
    async loadCartOffline(): Promise<void> {
        try {
            await this.indexedDB.init();
            const cart = await this.indexedDB.getCart(this.cartKey);
            
            if (!cart || !cart.items.length) {
                return; // No offline cart to load
            }
            
            // Clear server cart first
            await this.clearServerCart();
            
            // Load offline items to server cart
            for (const item of cart.items) {
                await this.addToServerCart(item);
            }
        } catch (error) {
            console.error('Error loading cart from offline:', error);
            throw error;
        }
    }
    
    private async getServerCart(): Promise<CartItem[]> {
        const response = await fetch('/api/cart');
        if (!response.ok) {
            throw new Error(`Failed to get server cart: ${response.statusText}`);
        }
        return await response.json();
    }
    
    private async syncToServer(items: CartItem[]): Promise<void> {
        // Clear server cart first
        await this.clearServerCart();
        
        // Add all items to server cart
        for (const item of items) {
            await this.addToServerCart(item);
        }
    }
    
    private async clearServerCart(): Promise<void> {
        const response = await fetch('/api/cart/clear', {
            method: 'POST'
        });
        if (!response.ok) {
            throw new Error(`Failed to clear server cart: ${response.statusText}`);
        }
    }
    
    private async addToServerCart(item: CartItem): Promise<void> {
        const response = await fetch('/api/cart/items', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(item)
        });
        if (!response.ok) {
            throw new Error(`Failed to add item to server cart: ${response.statusText}`);
        }
    }
    
    private mergeCartItems(offlineItems: CartItem[], serverItems: CartItem[]): CartItem[] {
        const mergedItems: CartItem[] = [];
        const processedProductIds = new Set<string>();
        
        // Add offline items first
        for (const offlineItem of offlineItems) {
            mergedItems.push(offlineItem);
            processedProductIds.add(offlineItem.productId);
        }
        
        // Add server items that aren't in offline
        for (const serverItem of serverItems) {
            if (!processedProductIds.has(serverItem.productId)) {
                mergedItems.push(serverItem);
            }
        }
        
        return mergedItems;
    }
}

export interface CartItem {
    productId: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
}

export interface SyncResult {
    success: boolean;
    syncedCount: number;
    errorCount: number;
    errors: string[];
}

export interface CartStorage {
    id: string;
    items: CartItem[];
    updatedAt: string;
}
