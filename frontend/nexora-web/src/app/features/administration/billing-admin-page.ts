import { Component, inject, signal } from '@angular/core';
import { SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  AdministrationService,
  BillingInvoice,
  BillingPayment,
  PlanPrice,
} from './administration.service';
import { NxBadge, NxButton, NxCard, NxDataTable, NxFormField, NxPageHeader } from '../../shared/ui';

@Component({
  selector: 'app-billing-admin',
  imports: [
    FormsModule,
    RouterLink,
    SlicePipe,
    NxPageHeader,
    NxCard,
    NxFormField,
    NxButton,
    NxDataTable,
    NxBadge,
  ],
  templateUrl: './billing-admin-page.html',
  styleUrl: './billing-admin-page.scss',
})
export class BillingAdminPage {
  private api = inject(AdministrationService);
  prices = signal<PlanPrice[]>([]);
  invoices = signal<BillingInvoice[]>([]);
  payments = signal<BillingPayment[]>([]);
  planId = '';
  interval = 'Monthly';
  amount = 0;
  constructor() {
    this.load();
  }
  badge(status: string): 'success' | 'danger' | 'warning' {
    const s = (status || '').toLowerCase();
    return s.includes('approv') || s.includes('paid')
      ? 'success'
      : s.includes('fail') || s.includes('reject')
        ? 'danger'
        : 'warning';
  }
  load() {
    this.api.prices().subscribe((x) => this.prices.set(x));
    this.api.invoices().subscribe((x) => this.invoices.set(x));
    this.api.payments().subscribe({ next: (x) => this.payments.set(x), error: () => {} });
  }
  save() {
    this.api.setPrice(this.planId, this.interval, this.amount).subscribe(() => this.load());
  }
}
