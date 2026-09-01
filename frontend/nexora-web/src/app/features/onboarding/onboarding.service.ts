import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { APP_CONFIG } from '../../core/config/app-config';

export type BillingInterval = 'Monthly' | 'Yearly';
export interface OnboardingDraft { id:string; currentStep:number; companyName:string|null; companySlug:string|null; segmentId:string|null; planId:string|null; billingInterval:BillingInterval|null; timeZoneId:string|null; status:'InProgress'|'Completed'|'Expired'; completedTenantId:string|null; expiresAt:string; }
export interface OnboardingSegment { id:string; code:string; name:string; }
export interface OnboardingPlan { planId:string; code:string; name:string; billingInterval:BillingInterval; amount:number; currency:string; limits:{featureCode:string;limit:number|null}[]; }
export interface OnboardingCompletion { tenantId:string; tenantSlug:string; subscriptionId:string; trialEndAt:string; }

@Injectable({providedIn:'root'})
export class OnboardingService {
  private readonly http=inject(HttpClient); private readonly config=inject(APP_CONFIG);
  start():Observable<OnboardingDraft>{return this.http.post<OnboardingDraft>(`${this.config.apiBaseUrl}/onboarding/drafts`,{});}
  update(draft:OnboardingDraft):Observable<OnboardingDraft>{return this.http.put<OnboardingDraft>(`${this.config.apiBaseUrl}/onboarding/drafts/${draft.id}`,draft);}
  segments():Observable<OnboardingSegment[]>{return this.http.get<OnboardingSegment[]>(`${this.config.apiBaseUrl}/onboarding/segments`);}
  plans():Observable<OnboardingPlan[]>{return this.http.get<OnboardingPlan[]>(`${this.config.apiBaseUrl}/onboarding/plans`);}
  complete(id:string):Observable<OnboardingCompletion>{return this.http.post<OnboardingCompletion>(`${this.config.apiBaseUrl}/onboarding/drafts/${id}/complete`,{});}
}
