import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { APP_CONFIG } from '../../core/config/app-config';
import {
  NxAvatar,
  NxButton,
  NxChipFilter,
  NxConfirmDialog,
  NxDataTable,
  NxFormField,
  NxModal,
  NxPageHeader,
  NxSearchInput,
} from '../../shared/ui';

interface Customer {
  id: string;
  name: string;
  phone?: string;
  email?: string;
  notes?: string;
}
interface Page {
  items: Customer[];
  total: number;
}

@Component({
  selector: 'app-customer-page',
  imports: [
    FormsModule,
    NxPageHeader,
    NxButton,
    NxSearchInput,
    NxChipFilter,
    NxDataTable,
    NxAvatar,
    NxModal,
    NxConfirmDialog,
    NxFormField,
  ],
  templateUrl: './customer-page.html',
  styleUrl: './customer-page.scss',
})
export class CustomerPage {
  private http = inject(HttpClient);
  private cfg = inject(APP_CONFIG);
  customers = signal<Customer[]>([]);
  total = signal(0);
  loading = signal(true);
  modalOpen = signal(false);
  confirming = signal<Customer | null>(null);
  saving = signal(false);
  search = '';
  filter = signal('all');
  form: any = {};

  readonly filterOptions = [
    { value: 'all', label: 'Todos' },
    { value: 'with-email', label: 'Com e-mail' },
    { value: 'with-phone', label: 'Com telefone' },
  ];

  readonly visible = computed(() => {
    const f = this.filter();
    return this.customers().filter((c) =>
      f === 'with-email' ? !!c.email : f === 'with-phone' ? !!c.phone : true,
    );
  });

  constructor() {
    this.load();
  }
  load() {
    this.loading.set(true);
    this.http
      .get<Page>(`${this.cfg.apiBaseUrl}/customers?search=${encodeURIComponent(this.search)}`)
      .subscribe({
        next: (x) => {
          this.customers.set(x.items);
          this.total.set(x.total);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }
  onSearch(term: string) {
    this.search = term;
    this.load();
  }
  newCustomer() {
    this.form = { name: '', phone: '', email: '', notes: '' };
    this.modalOpen.set(true);
  }
  edit(c: Customer) {
    this.form = { ...c };
    this.modalOpen.set(true);
  }
  save() {
    if (!this.form.name?.trim() || this.saving()) return;
    this.saving.set(true);
    const req = this.form.id
      ? this.http.put(`${this.cfg.apiBaseUrl}/customers/${this.form.id}`, this.form)
      : this.http.post(`${this.cfg.apiBaseUrl}/customers`, this.form);
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.modalOpen.set(false);
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }
  remove() {
    const c = this.confirming();
    if (!c) return;
    this.http.delete(`${this.cfg.apiBaseUrl}/customers/${c.id}`).subscribe(() => {
      this.confirming.set(null);
      this.load();
    });
  }
}
