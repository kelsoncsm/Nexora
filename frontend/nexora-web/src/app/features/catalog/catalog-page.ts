import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { APP_CONFIG } from '../../core/config/app-config';

@Component({
  selector: 'app-catalog-page',
  imports: [FormsModule],
  templateUrl: './catalog-page.html',
  styleUrl: './catalog-page.scss',
})
export class CatalogPage {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  kind = inject(ActivatedRoute).snapshot.data['kind'] as string;
  items = signal<any[]>([]);
  editing = signal(false);
  form: any = {};
  professionalId = '';
  serviceId = '';
  constructor() {
    this.load();
  }
  initials(name: string) {
    const p = (name || '').trim().split(/\s+/);
    return ((p[0]?.[0] ?? '?') + (p[1]?.[0] ?? '')).toUpperCase();
  }
  startNew() {
    this.form = {};
    this.editing.set(true);
  }
  load() {
    this.http
      .get<any[]>(`${this.config.apiBaseUrl}/${this.kind}`)
      .subscribe((x) => this.items.set(x));
  }
  edit(x: any) {
    this.form = { ...x };
    this.editing.set(true);
  }
  save() {
    const body =
      this.kind === 'services'
        ? {
            ...this.form,
            durationMinutes: +this.form.durationMinutes,
            price: +this.form.price,
            isActive: true,
          }
        : { ...this.form, isActive: true };
    const req = this.form.id
      ? this.http.put(`${this.config.apiBaseUrl}/${this.kind}/${this.form.id}`, body)
      : this.http.post(`${this.config.apiBaseUrl}/${this.kind}`, body);
    req.subscribe(() => {
      this.editing.set(false);
      this.form = {};
      this.load();
    });
  }
  remove(id: string) {
    this.http.delete(`${this.config.apiBaseUrl}/${this.kind}/${id}`).subscribe(() => this.load());
  }
  link() {
    this.http
      .put(
        `${this.config.apiBaseUrl}/professionals/${this.professionalId}/services/${this.serviceId}`,
        {},
      )
      .subscribe(() => this.load());
  }
}
