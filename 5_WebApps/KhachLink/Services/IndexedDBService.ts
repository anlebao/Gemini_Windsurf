export interface IndexedDBService {
    init(): Promise<void>;
    storeOrder(order: Order): Promise<void>;
    getOrder(orderId: Guid): Promise<Order | null>;
    getOrders(): Promise<Order[]>;
    storeSyncItem(item: SyncItem): Promise<void>;
    getPendingSync(): Promise<SyncItem[]>;
    markSynced(orderId: Guid): Promise<void>;
}

export class IndexedDBServiceImpl implements IndexedDBService {
    private db: IDBDatabase | null = null;
    private readonly dbName = 'VanAnKhachLink';
    private readonly version = 1;
    
    async init(): Promise<void> {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(this.dbName, this.version);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => {
                this.db = request.result;
                resolve();
            };
            
            request.onupgradeneeded = (event) => {
                const db = (event.target as IDBOpenDBRequest).result;
                
                // Create object stores
                if (!db.objectStoreNames.contains('orders')) {
                    const orderStore = db.createObjectStore('orders', { keyPath: 'id' });
                    orderStore.createIndex('createdAt', 'createdAt');
                    orderStore.createIndex('status', 'status');
                }
                
                if (!db.objectStoreNames.contains('sync')) {
                    const syncStore = db.createObjectStore('sync', { keyPath: 'id' });
                    syncStore.createIndex('orderId', 'orderId');
                    syncStore.createIndex('status', 'status');
                }
            };
        });
    }
    
    async storeOrder(order: Order): Promise<void> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['orders'], 'readwrite');
            const store = transaction.objectStore('orders');
            const request = store.put(order);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve();
        });
    }
    
    async getOrder(orderId: Guid): Promise<Order | null> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['orders'], 'readonly');
            const store = transaction.objectStore('orders');
            const request = store.get(orderId);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result || null);
        });
    }
    
    async getOrders(): Promise<Order[]> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['orders'], 'readonly');
            const store = transaction.objectStore('orders');
            const request = store.getAll();
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result || []);
        });
    }
    
    async storeSyncItem(item: SyncItem): Promise<void> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['sync'], 'readwrite');
            const store = transaction.objectStore('sync');
            const request = store.put(item);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve();
        });
    }
    
    async getPendingSync(): Promise<SyncItem[]> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['sync'], 'readonly');
            const store = transaction.objectStore('sync');
            const index = store.index('status');
            const request = index.getAll('pending');
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result || []);
        });
    }
    
    async markSynced(orderId: Guid): Promise<void> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['sync'], 'readwrite');
            const store = transaction.objectStore('sync');
            const index = store.index('orderId');
            const request = index.openCursor(IDBKeyRange.only(orderId));
            
            request.onerror = () => reject(request.error);
            request.onsuccess = (event) => {
                const cursor = (event.target as IDBRequest).result;
                if (cursor) {
                    const syncItem = cursor.value as SyncItem;
                    syncItem.status = 'synced';
                    syncItem.syncedAt = new Date().toISOString();
                    cursor.update(syncItem);
                    cursor.continue();
                } else {
                    resolve();
                }
            };
        });
    }
}

export interface Order {
    id: Guid;
    customerId: string;
    shopId: string;
    items: OrderItem[];
    totalAmount: number;
    status: string;
    createdAt: string;
    updatedAt?: string;
}

export interface OrderItem {
    productId: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
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
