import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

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

export interface OrderMetrics {
    totalOrders: number;
    pendingOrders: number;
    processingOrders: number;
    completedOrders: number;
    cancelledOrders: number;
    totalRevenue: number;
    averageOrderValue: number;
    ordersPerHour: number;
    revenuePerHour: number;
    statusBreakdown: StatusCount[];
}

export interface StatusCount {
    status: string;
    count: number;
    percentage: number;
}

export interface OrderSummary {
    orderId: string;
    customerId: string;
    status: string;
    createdAt: string;
    updatedAt?: string;
    totalAmount: number;
    itemCount: number;
    items: OrderItemSummary[];
    statusHistory: OrderStatusHistory[];
}

export interface OrderItemSummary {
    productId: string;
    productName: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
}

export interface OrderStatusHistory {
    status: string;
    changedAt: string;
    reason?: string;
    changedBy?: string;
}

@Injectable({
    providedIn: 'root'
})
export class OrderManagementService {
    private readonly apiUrl = '/api/orders';

    constructor(private http: HttpClient) {}

    getOrders(status?: string): Observable<Order[]> {
        const url = status ? `${this.apiUrl}?status=${status}` : this.apiUrl;
        return this.http.get<Order[]>(url);
    }

    getOrder(orderId: string): Observable<Order> {
        return this.http.get<Order>(`${this.apiUrl}/${orderId}`);
    }

    updateOrderStatus(orderId: string, newStatus: string, reason?: string): Observable<boolean> {
        const body = reason ? { status: newStatus, reason } : { status: newStatus };
        return this.http.put<boolean>(`${this.apiUrl}/${orderId}/status`, body);
    }

    getOrdersByDateRange(startDate: Date, endDate: Date): Observable<Order[]> {
        const params = {
            startDate: startDate.toISOString(),
            endDate: endDate.toISOString()
        };
        return this.http.get<Order[]>(`${this.apiUrl}/daterange`, { params });
    }

    getOrderMetrics(): Observable<OrderMetrics> {
        return this.http.get<OrderMetrics>(`${this.apiUrl}/metrics`);
    }

    assignOrderToStaff(orderId: string, staffId: string): Observable<boolean> {
        return this.http.put<boolean>(`${this.apiUrl}/${orderId}/assign`, { staffId });
    }

    getOrdersByCustomer(customerId: string): Observable<Order[]> {
        return this.http.get<Order[]>(`${this.apiUrl}/customer/${customerId}`);
    }

    cancelOrder(orderId: string, reason: string): Observable<boolean> {
        return this.http.put<boolean>(`${this.apiUrl}/${orderId}/cancel`, { reason });
    }

    getOrderSummary(orderId: string): Observable<OrderSummary> {
        return this.http.get<OrderSummary>(`${this.apiUrl}/${orderId}/summary`);
    }
}
