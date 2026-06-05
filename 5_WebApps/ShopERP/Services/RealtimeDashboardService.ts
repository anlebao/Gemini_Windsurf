import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject, BehaviorSubject } from 'rxjs';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Order, OrderMetrics } from './order-management.service';

export interface DashboardAlert {
    id: string;
    type: AlertType;
    title: string;
    message: string;
    severity: AlertSeverity;
    createdAt: Date;
    expiresAt?: Date;
    isRead: boolean;
    actionUrl?: string;
    metadata: Record<string, any>;
}

export enum AlertType {
    OrderCreated = 'orderCreated',
    OrderUpdated = 'orderUpdated',
    OrderCancelled = 'orderCancelled',
    HighValueOrder = 'highValueOrder',
    SystemError = 'systemError',
    LowInventory = 'lowInventory',
    StaffAssignment = 'staffAssignment'
}

export enum AlertSeverity {
    Info = 'info',
    Warning = 'warning',
    Error = 'error',
    Critical = 'critical'
}

@Injectable({
    providedIn: 'root'
})
export class RealtimeDashboardService {
    private hubConnection: HubConnection | null = null;
    private readonly apiUrl = '/api/dashboard';
    private readonly hubUrl = '/hubs/order';
    
    // Observables for real-time updates
    private orderUpdateSubject = new Subject<Order>();
    private metricsUpdateSubject = new BehaviorSubject<OrderMetrics | null>(null);
    private alertSubject = new Subject<DashboardAlert>();
    
    // Public observables
    public orderUpdates$ = this.orderUpdateSubject.asObservable();
    public metricsUpdates$ = this.metricsUpdateSubject.asObservable();
    public alerts$ = this.alertSubject.asObservable();
    
    private subscribedConnections = new Set<string>();
    
    constructor(private http: HttpClient) {}

    async getCurrentMetrics(): Promise<OrderMetrics> {
        try {
            const response = await this.http.get<OrderMetrics>(`${this.apiUrl}/metrics`).toPromise();
            if (response) {
                this.metricsUpdateSubject.next(response);
            }
            return response || this.getDefaultMetrics();
        } catch (error) {
            console.error('Failed to get current dashboard metrics:', error);
            return this.getDefaultMetrics();
        }
    }

    async getRecentOrders(count: number = 10): Promise<Order[]> {
        try {
            return await this.http.get<Order[]>(`${this.apiUrl}/orders/recent?count=${count}`).toPromise() || [];
        } catch (error) {
            console.error('Failed to get recent orders:', error);
            return [];
        }
    }

    async getActiveOrders(): Promise<Order[]> {
        try {
            return await this.http.get<Order[]>(`${this.apiUrl}/orders/active`).toPromise() || [];
        } catch (error) {
            console.error('Failed to get active orders:', error);
            return [];
        }
    }

    async broadcastOrderUpdate(order: Order): Promise<boolean> {
        try {
            if (this.subscribedConnections.size === 0) {
                return false; // No subscribers
            }

            // Invalidate relevant caches (handled by server)
            
            // Create alert for high-value orders
            if (order.totalAmount > 1000000) { // 1 million VND
                await this.createAlert({
                    type: AlertType.HighValueOrder,
                    title: 'High Value Order',
                    message: `Order ${order.id} with total ${order.totalAmount.toLocaleString()} VND`,
                    severity: AlertSeverity.Info,
                    metadata: {
                        orderId: order.id,
                        totalAmount: order.totalAmount
                    }
                });
            }

            // Broadcast to all subscribed clients (handled by SignalR hub)
            this.orderUpdateSubject.next(order);

            // Also broadcast updated metrics
            await this.broadcastMetricsUpdate();

            console.log('Broadcasted order update:', order.id);
            return true;
        } catch (error) {
            console.error('Failed to broadcast order update:', error);
            return false;
        }
    }

    async broadcastMetricsUpdate(): Promise<boolean> {
        try {
            if (this.subscribedConnections.size === 0) {
                return false; // No subscribers
            }

            const metrics = await this.getCurrentMetrics();
            this.metricsUpdateSubject.next(metrics);

            console.log('Broadcasted metrics update');
            return true;
        } catch (error) {
            console.error('Failed to broadcast metrics update:', error);
            return false;
        }
    }

    async subscribeToUpdates(connectionId: string): Promise<boolean> {
        try {
            this.subscribedConnections.add(connectionId);

            // Send initial data
            const metrics = await this.getCurrentMetrics();
            const recentOrders = await this.getRecentOrders(10);
            const activeOrders = await this.getActiveOrders();
            const alerts = await this.getActiveAlerts();

            console.log('Client subscribed to dashboard updates:', connectionId);
            return true;
        } catch (error) {
            console.error('Failed to subscribe client to updates:', connectionId, error);
            return false;
        }
    }

    async unsubscribeFromUpdates(connectionId: string): Promise<boolean> {
        try {
            this.subscribedConnections.delete(connectionId);
            console.log('Client unsubscribed from dashboard updates:', connectionId);
            return true;
        } catch (error) {
            console.error('Failed to unsubscribe client from updates:', connectionId, error);
            return false;
        }
    }

    async getActiveAlerts(): Promise<DashboardAlert[]> {
        try {
            return await this.http.get<DashboardAlert[]>(`${this.apiUrl}/alerts`).toPromise() || [];
        } catch (error) {
            console.error('Failed to get active alerts:', error);
            return [];
        }
    }

    async createAlert(alert: Partial<DashboardAlert>): Promise<boolean> {
        try {
            const fullAlert: DashboardAlert = {
                id: this.generateGuid(),
                type: alert.type || AlertType.SystemError,
                title: alert.title || '',
                message: alert.message || '',
                severity: alert.severity || AlertSeverity.Info,
                createdAt: new Date(),
                expiresAt: alert.expiresAt,
                isRead: false,
                actionUrl: alert.actionUrl,
                metadata: alert.metadata || {}
            };

            // Broadcast alert to all subscribed clients
            if (this.subscribedConnections.size > 0) {
                this.alertSubject.next(fullAlert);
            }

            console.log('Created dashboard alert:', alert.type);
            return true;
        } catch (error) {
            console.error('Failed to create dashboard alert:', alert.type, error);
            return false;
        }
    }

    // SignalR connection management
    async startConnection(): Promise<void> {
        try {
            this.hubConnection = new HubConnectionBuilder()
                .withUrl(this.hubUrl)
                .withAutomaticReconnect()
                .configureLogging(LogLevel.Information)
                .build();

            this.hubConnection.on('OrderUpdated', (order) => {
                this.orderUpdateSubject.next(order);
            });

            this.hubConnection.on('MetricsUpdated', (metrics) => {
                this.metricsUpdateSubject.next(metrics);
            });

            this.hubConnection.on('AlertCreated', (alert) => {
                this.alertSubject.next(alert);
            });

            await this.hubConnection.start();
            console.log('SignalR connection started');
        } catch (error) {
            console.error('Failed to start SignalR connection:', error);
        }
    }

    async stopConnection(): Promise<void> {
        try {
            if (this.hubConnection) {
                await this.hubConnection.stop();
                this.hubConnection = null;
                console.log('SignalR connection stopped');
            }
        } catch (error) {
            console.error('Failed to stop SignalR connection:', error);
        }
    }

    private getDefaultMetrics(): OrderMetrics {
        return {
            totalOrders: 0,
            pendingOrders: 0,
            processingOrders: 0,
            completedOrders: 0,
            cancelledOrders: 0,
            totalRevenue: 0,
            averageOrderValue: 0,
            ordersPerHour: 0,
            revenuePerHour: 0,
            statusBreakdown: []
        };
    }

    private generateGuid(): string {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }
}
