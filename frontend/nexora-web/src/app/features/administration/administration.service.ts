import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { APP_CONFIG } from '../../core/config/app-config';

export interface AdminDashboard { totalTenants: number; activeTenants: number; totalUsers: number; activeSegments: number; }
export interface AdminTenant { id: string; name: string; slug: string; isActive: boolean; createdAt: string; }
export interface AdminUser { id: string; email: string; isActive: boolean; createdAt: string; }
export interface Segment { id: string; code: string; name: string; isActive: boolean; createdAt: string; }
export interface AuditLog { id: string; actorUserId: string; action: string; targetType: string; targetId: string; occurredAt: string; }
export interface Feature { id:string; code:string; name:string; isActive:boolean; }
export interface Plan { id:string; code:string; name:string; isActive:boolean; isPublic:boolean; isTrialEligible:boolean; features:{featureId:string;featureCode:string;enabled:boolean;limit:number|null}[]; }
export interface Subscription { id:string;tenantId:string;planId:string;planCode:string;status:string;billingInterval:string;trialEndAt:string;currentPeriodEnd:string;cancelAtPeriodEnd:boolean;pastDueSince:string|null; }
export interface PlanPrice { id:string;planId:string;billingInterval:string;currency:string;amount:number;isActive:boolean;createdAt:string; }
export interface BillingInvoice { id:string;tenantId:string;planId:string;billingInterval:string;amount:number;currency:string;status:string;dueAt:string; }

@Injectable({ providedIn: 'root' })
export class AdministrationService {
  private readonly http = inject(HttpClient); private readonly config = inject(APP_CONFIG);
  private readonly base = `${this.config.apiBaseUrl}/admin`;
  dashboard() { return this.http.get<AdminDashboard>(`${this.base}/dashboard`); }
  tenants() { return this.http.get<AdminTenant[]>(`${this.base}/tenants`); }
  users() { return this.http.get<AdminUser[]>(`${this.base}/users`); }
  segments() { return this.http.get<Segment[]>(`${this.base}/segments`); }
  auditLogs() { return this.http.get<AuditLog[]>(`${this.base}/audit-logs`); }
  features() { return this.http.get<Feature[]>(`${this.base}/features`); }
  plans() { return this.http.get<Plan[]>(`${this.base}/plans`); }
  createFeature(code:string,name:string){return this.http.post<Feature>(`${this.base}/features`,{code,name});}
  createPlan(code:string,name:string){return this.http.post<Plan>(`${this.base}/plans`,{code,name});}
  configurePlan(plan:Plan,isPublic:boolean,isTrialEligible:boolean){return this.http.put<Plan>(`${this.base}/plans/${plan.id}`,{name:plan.name,isActive:plan.isActive,isPublic,isTrialEligible});}
  configurePlanFeature(planId:string,featureId:string,enabled:boolean,limit:number|null){return this.http.put(`${this.base}/plans/${planId}/features/${featureId}`,{enabled,limit});}
  subscriptions(){return this.http.get<Subscription[]>(`${this.base}/subscriptions`);}
  createSubscription(tenantId:string,planId:string,billingInterval:string){return this.http.post<Subscription>(`${this.base}/subscriptions`,{tenantId,planId,billingInterval});}
  subscriptionAction(id:string,action:string,body:object={}){return this.http.post<Subscription>(`${this.base}/subscriptions/${id}/${action}`,body);}
  prices(){return this.http.get<PlanPrice[]>(`${this.base}/billing/prices`);}
  setPrice(planId:string,billingInterval:string,amount:number){return this.http.post<PlanPrice>(`${this.base}/billing/prices`,{planId,billingInterval,currency:'BRL',amount});}
  invoices(){return this.http.get<BillingInvoice[]>(`${this.base}/billing/invoices`);}
}
