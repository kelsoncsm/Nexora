import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { APP_CONFIG } from '../../core/config/app-config';

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
  imports: [FormsModule],
  templateUrl: './customer-page.html',
  styleUrl: './customer-page.scss',
})
export class CustomerPage {
  private http = inject(HttpClient);
  private cfg = inject(APP_CONFIG);
  customers = signal<Customer[]>([]);
  total = signal(0);
  editing = signal(false);
  search = '';
  form: any = {};
  constructor() {
    this.load();
  }
  initials(name: string) {
    const p = (name || '').trim().split(/\s+/);
    return ((p[0]?.[0] ?? '?') + (p[1]?.[0] ?? '')).toUpperCase();
  }
  load() {
    this.http
      .get<Page>(`${this.cfg.apiBaseUrl}/customers?search=${encodeURIComponent(this.search)}`)
      .subscribe((x) => {
        this.customers.set(x.items);
        this.total.set(x.total);
      });
  }
  newCustomer() {
    this.form = { name: '', phone: '', email: '', notes: '' };
    this.editing.set(true);
  }
  edit(c: Customer) {
    this.form = { ...c };
    this.editing.set(true);
  }
  save() {
    const req = this.form.id
      ? this.http.put(`${this.cfg.apiBaseUrl}/customers/${this.form.id}`, this.form)
      : this.http.post(`${this.cfg.apiBaseUrl}/customers`, this.form);
    req.subscribe(() => {
      this.editing.set(false);
      this.load();
    });
  }
  remove(id: string) {
    this.http.delete(`${this.cfg.apiBaseUrl}/customers/${id}`).subscribe(() => this.load());
  }
}
