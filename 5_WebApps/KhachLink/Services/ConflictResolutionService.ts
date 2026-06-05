export interface ConflictResolutionService {
    resolveOrderConflict(offlineOrder: Order, serverOrder?: Order): Promise<ConflictResolution>;
    resolveCartConflict(offlineItems: CartItem[], serverItems: CartItem[]): Promise<ConflictResolution>;
    validateOrder(order: Order): Promise<boolean>;
    validateCart(items: CartItem[]): Promise<boolean>;
    generateConflictReport(offlineOrder: Order, serverOrder?: Order): Promise<ConflictReport>;
}

export class ConflictResolutionServiceImpl implements ConflictResolutionService {
    async resolveOrderConflict(offlineOrder: Order, serverOrder?: Order): Promise<ConflictResolution> {
        const result: ConflictResolution = {
            success: false,
            action: 'error',
            reason: '',
            mergedOrder: null,
            mergedItems: null,
            warnings: []
        };
        
        try {
            // Validate offline order first
            if (!await this.validateOrder(offlineOrder)) {
                result.action = 'error';
                result.reason = 'Offline order validation failed';
                return result;
            }
            
            // Case 1: No server order exists - use offline
            if (!serverOrder) {
                result.success = true;
                result.action = 'useOffline';
                result.reason = 'No server order found';
                result.mergedOrder = offlineOrder;
                return result;
            }
            
            // Case 2: Server order is newer - use server
            if (new Date(serverOrder.createdAt) > new Date(offlineOrder.createdAt)) {
                result.success = true;
                result.action = 'useServer';
                result.reason = 'Server order is newer';
                result.mergedOrder = serverOrder;
                return result;
            }
            
            // Case 3: Same timestamp - merge items
            const timeDiff = Math.abs(new Date(offlineOrder.createdAt).getTime() - new Date(serverOrder.createdAt).getTime());
            if (timeDiff < 5000) { // 5 seconds
                const mergedOrder = await this.mergeOrders(offlineOrder, serverOrder);
                result.success = true;
                result.action = 'merge';
                result.reason = 'Orders created at same time - merging items';
                result.mergedOrder = mergedOrder;
                return result;
            }
            
            // Case 4: Offline order is newer - use offline
            result.success = true;
            result.action = 'useOffline';
            result.reason = 'Offline order is newer';
            result.mergedOrder = offlineOrder;
            return result;
        } catch (error) {
            result.success = false;
            result.action = 'error';
            result.reason = error.message;
            return result;
        }
    }
    
    async resolveCartConflict(offlineItems: CartItem[], serverItems: CartItem[]): Promise<ConflictResolution> {
        const result: ConflictResolution = {
            success: false,
            action: 'error',
            reason: '',
            mergedOrder: null,
            mergedItems: [],
            warnings: []
        };
        
        try {
            // Validate cart items
            if (!await this.validateCart(offlineItems)) {
                result.action = 'error';
                result.reason = 'Offline cart validation failed';
                return result;
            }
            
            // Merge cart items - combine unique items
            const mergedItems = this.mergeCartItems(offlineItems, serverItems);
            
            result.success = true;
            result.action = 'merge';
            result.reason = 'Cart items merged successfully';
            result.mergedItems = mergedItems;
            
            // Add warnings for potential issues
            this.checkCartWarnings(mergedItems, result);
            
            return result;
        } catch (error) {
            result.success = false;
            result.action = 'error';
            result.reason = error.message;
            return result;
        }
    }
    
    async validateOrder(order: Order): Promise<boolean> {
        try {
            // Basic validation
            if (!order.id || !order.customerId || !order.shopId) {
                return false;
            }
            
            if (!order.items || order.items.length === 0) {
                return false;
            }
            
            // Validate items
            for (const item of order.items) {
                if (!item.productId || item.quantity <= 0 || item.unitPrice <= 0) {
                    return false;
                }
                
                if (Math.abs(item.totalPrice - (item.quantity * item.unitPrice)) > 0.01) {
                    return false;
                }
            }
            
            // Validate total amount
            const calculatedTotal = order.items.reduce((sum, item) => sum + item.totalPrice, 0);
            if (Math.abs(calculatedTotal - order.totalAmount) > 0.01) {
                return false;
            }
            
            return true;
        } catch (error) {
            console.error('Order validation failed:', error);
            return false;
        }
    }
    
    async validateCart(items: CartItem[]): Promise<boolean> {
        try {
            if (!items || items.length === 0) {
                return true; // Empty cart is valid
            }
            
            for (const item of items) {
                if (!item.productId || item.quantity <= 0 || item.unitPrice <= 0) {
                    return false;
                }
                
                if (Math.abs(item.totalPrice - (item.quantity * item.unitPrice)) > 0.01) {
                    return false;
                }
            }
            
            return true;
        } catch (error) {
            console.error('Cart validation failed:', error);
            return false;
        }
    }
    
    async generateConflictReport(offlineOrder: Order, serverOrder?: Order): Promise<ConflictReport> {
        const report: ConflictReport = {
            orderId: offlineOrder.id,
            hasConflict: false,
            conflicts: [],
            warnings: [],
            recommendedAction: 'useOffline',
            recommendation: ''
        };
        
        try {
            if (!serverOrder) {
                report.recommendedAction = 'useOffline';
                report.recommendation = 'No server order found - use offline order';
                return report;
            }
            
            // Check for conflicts
            const timeDiff = Math.abs(new Date(offlineOrder.createdAt).getTime() - new Date(serverOrder.createdAt).getTime());
            
            if (timeDiff > 300000) { // 5 minutes
                report.conflicts.push(`Timestamp mismatch: Offline ${offlineOrder.createdAt}, Server ${serverOrder.createdAt}`);
                report.hasConflict = true;
            }
            
            if (Math.abs(offlineOrder.totalAmount - serverOrder.totalAmount) > 0.01) {
                report.conflicts.push(`Total amount mismatch: Offline ${offlineOrder.totalAmount}, Server ${serverOrder.totalAmount}`);
                report.hasConflict = true;
            }
            
            // Check items differences
            const offlineProductIds = offlineOrder.items.map(i => i.productId);
            const serverProductIds = serverOrder.items.map(i => i.productId);
            
            if (!this.arraysEqual(offlineProductIds, serverProductIds)) {
                report.conflicts.push('Items differ between offline and server orders');
                report.hasConflict = true;
            }
            
            // Status conflict
            if (offlineOrder.status !== serverOrder.status) {
                report.warnings.push(`Status differs: Offline ${offlineOrder.status}, Server ${serverOrder.status}`);
            }
            
            // Recommend action
            if (report.hasConflict) {
                if (new Date(offlineOrder.createdAt) > new Date(serverOrder.createdAt)) {
                    report.recommendedAction = 'useOffline';
                    report.recommendation = 'Offline order is newer - prefer offline version';
                } else {
                    report.recommendedAction = 'merge';
                    report.recommendation = 'Server order is newer - merge items to preserve data';
                }
            } else {
                report.recommendedAction = 'useServer';
                report.recommendation = 'No significant conflicts - use server version';
            }
        } catch (error) {
            report.hasConflict = true;
            report.recommendedAction = 'error';
            report.recommendation = error.message;
        }
        
        return report;
    }
    
    private async mergeOrders(offlineOrder: Order, serverOrder: Order): Promise<Order> {
        // Create merged order with server data as base
        const mergedOrder: Order = {
            id: serverOrder.id,
            customerId: serverOrder.customerId,
            shopId: serverOrder.shopId,
            totalAmount: serverOrder.totalAmount,
            status: serverOrder.status,
            createdAt: serverOrder.createdAt,
            items: []
        };
        
        // Merge items from both orders
        const processedProductIds = new Set<string>();
        
        // Add server items first
        for (const serverItem of serverOrder.items) {
            mergedOrder.items.push({
                productId: serverItem.productId,
                quantity: serverItem.quantity,
                unitPrice: serverItem.unitPrice,
                totalPrice: serverItem.totalPrice
            });
            processedProductIds.add(serverItem.productId);
        }
        
        // Add offline items that aren't in server
        for (const offlineItem of offlineOrder.items) {
            if (!processedProductIds.has(offlineItem.productId)) {
                mergedOrder.items.push({
                    productId: offlineItem.productId,
                    quantity: offlineItem.quantity,
                    unitPrice: offlineItem.unitPrice,
                    totalPrice: offlineItem.totalPrice
                });
            }
        }
        
        // Recalculate total
        mergedOrder.totalAmount = mergedOrder.items.reduce((sum, item) => sum + item.totalPrice, 0);
        
        return mergedOrder;
    }
    
    private mergeCartItems(offlineItems: CartItem[], serverItems: CartItem[]): CartItem[] {
        const mergedItems: CartItem[] = [];
        const processedProductIds = new Set<string>();
        
        // Add offline items first
        for (const offlineItem of offlineItems) {
            mergedItems.push(offlineItem);
            processedProductIds.add(offlineItem.productId);
        }
        
        // Add server items not in offline
        for (const serverItem of serverItems) {
            if (!processedProductIds.has(serverItem.productId)) {
                mergedItems.push({
                    productId: serverItem.productId,
                    quantity: serverItem.quantity,
                    unitPrice: serverItem.unitPrice,
                    totalPrice: serverItem.totalPrice
                });
            }
        }
        
        return mergedItems;
    }
    
    private checkCartWarnings(items: CartItem[], result: ConflictResolution): void {
        if (items.length > 20) {
            result.warnings.push('Large number of items in cart may affect performance');
        }
        
        const totalValue = items.reduce((sum, item) => sum + item.totalPrice, 0);
        if (totalValue > 1000000) { // 1 million VND
            result.warnings.push('High-value cart - consider confirming with customer');
        }
        
        const highQuantityItems = items.filter(item => item.quantity > 10);
        if (highQuantityItems.length > 0) {
            result.warnings.push(`Items with high quantity: ${highQuantityItems.map(i => i.productId).join(', ')}`);
        }
    }
    
    private arraysEqual(a: string[], b: string[]): boolean {
        if (a.length !== b.length) return false;
        const sortedA = [...a].sort();
        const sortedB = [...b].sort();
        return sortedA.every((val, index) => val === sortedB[index]);
    }
}

export interface ConflictResolution {
    success: boolean;
    action: 'useOffline' | 'useServer' | 'merge' | 'skip' | 'error';
    reason: string;
    mergedOrder?: Order;
    mergedItems?: CartItem[];
    warnings: string[];
}

export interface ConflictReport {
    orderId: string;
    hasConflict: boolean;
    conflicts: string[];
    warnings: string[];
    recommendedAction: 'useOffline' | 'useServer' | 'merge' | 'error';
    recommendation: string;
}

export interface Order {
    id: string;
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

export interface CartItem {
    productId: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
}
