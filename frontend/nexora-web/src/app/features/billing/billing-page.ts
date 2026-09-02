import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { APP_CONFIG } from '../../core/config/app-config';
import { NxBadge, NxButton, NxCard, NxDataTable, NxFormField, NxPageHeader } from '../../shared/ui';

interface Invoice {
  id: string;
  status: string;
  amount: number;
  currency: string;
  dueAt: string;
}
interface Checkout {
  invoiceId: string;
  amount: number;
  currency: string;
  status: string;
  checkoutUrl: string | null;
  qrCode: string | null;
}

@Component({
  selector: 'app-billing-page',
  imports: [FormsModule, NxPageHeader, NxCard, NxFormField, NxButton, NxDataTable, NxBadge],
  templateUrl: './billing-page.html',
  styleUrl: './billing-page.scss',
})
export class BillingPage {
  private http = inject(HttpClient);
  private base = inject(APP_CONFIG).apiBaseUrl;
  invoices = signal<Invoice[]>([]);
  result = signal<Checkout | null>(null);
  error = signal('');
  method = 'Pix';
  email = '';
  constructor() {
    this.load();
  }
  badge(status: string): 'success' | 'danger' | 'warning' {
    const s = (status || '').toLowerCase();
    return s.includes('paid') || s.includes('approved')
      ? 'success'
      : s.includes('fail') || s.includes('cancel')
        ? 'danger'
        : 'warning';
  }
  load() {
    this.http.get<Invoice[]>(`${this.base}/billing/invoices`).subscribe({
      next: (x) => {
        this.invoices.set(x);
        this.error.set('');
      },
      error: (e) =>
        this.error.set(
          e?.status === 403
            ? 'Você não tem permissão para gerenciar a assinatura desta empresa.'
            : 'Não foi possível carregar as cobranças.',
        ),
    });
  }
  checkout() {
    this.http
      .post<Checkout>(`${this.base}/billing/checkout`, {
        paymentMethod: this.method,
        payerEmail: this.email,
      })
      .subscribe({
        next: (x) => {
          this.result.set(x);
          this.load();
        },
        error: (e) =>
          this.error.set(
            e?.status === 403
              ? 'Você não tem permissão para iniciar um checkout.'
              : 'Não foi possível criar o checkout.',
          ),
      });
  }
}
