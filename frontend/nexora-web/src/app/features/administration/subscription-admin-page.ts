import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdministrationService, Subscription } from './administration.service';
import { NxBadge, NxButton, NxCard, NxEmptyState, NxFormField, NxPageHeader } from '../../shared/ui';

@Component({
  selector: 'app-subscription-admin-page',
  imports: [
    FormsModule,
    DatePipe,
    RouterLink,
    NxPageHeader,
    NxCard,
    NxFormField,
    NxButton,
    NxBadge,
    NxEmptyState,
  ],
  templateUrl: './subscription-admin-page.html',
  styleUrl: './subscription-admin-page.scss',
})
export class SubscriptionAdminPage {
  private api = inject(AdministrationService);
  subscriptions = signal<Subscription[]>([]);
  error = signal('');
  tenantId = '';
  planId = '';
  interval = 'Monthly';
  constructor() {
    this.load();
  }
  badge(status: string): 'success' | 'warning' | 'danger' | 'neutral' {
    const s = (status || '').toLowerCase();
    return s === 'active' || s === 'trialing'
      ? 'success'
      : s === 'pastdue'
        ? 'warning'
        : s === 'canceled' || s === 'expired'
          ? 'danger'
          : 'neutral';
  }
  load() {
    this.api
      .subscriptions()
      .subscribe({ next: (x) => this.subscriptions.set(x), error: () => this.fail() });
  }
  create() {
    this.api.createSubscription(this.tenantId, this.planId, this.interval).subscribe({
      next: () => {
        this.tenantId = '';
        this.planId = '';
        this.load();
      },
      error: () => this.fail(),
    });
  }
  act(item: Subscription, action: string) {
    this.api
      .subscriptionAction(item.id, action)
      .subscribe({ next: () => this.load(), error: () => this.fail() });
  }
  changePlan(item: Subscription) {
    const planId = window.prompt('Novo Plan ID', item.planId);
    if (planId)
      this.api
        .subscriptionAction(item.id, 'change-plan', { planId })
        .subscribe({ next: () => this.load(), error: () => this.fail() });
  }
  private fail() {
    this.error.set('Não foi possível concluir a operação.');
    setTimeout(() => this.error.set(''), 4000);
  }
}
