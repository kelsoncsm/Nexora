import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { APP_CONFIG } from '../../core/config/app-config';
import {
  NxAvatar,
  NxButton,
  NxConfirmDialog,
  NxDataTable,
  NxFormField,
  NxModal,
  NxPageHeader,
  NxSearchInput,
} from '../../shared/ui';

@Component({
  selector: 'app-catalog-page',
  imports: [
    FormsModule,
    NxPageHeader,
    NxButton,
    NxSearchInput,
    NxDataTable,
    NxAvatar,
    NxModal,
    NxConfirmDialog,
    NxFormField,
  ],
  templateUrl: './catalog-page.html',
  styleUrl: './catalog-page.scss',
})
export class CatalogPage {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  kind = inject(ActivatedRoute).snapshot.data['kind'] as string;
  isPros = this.kind === 'professionals';
  items = signal<any[]>([]);
  services = signal<any[]>([]);
  loading = signal(true);
  modalOpen = signal(false);
  confirming = signal<any | null>(null);
  saving = signal(false);
  linkOpen = signal(false);
  search = '';
  q = signal('');
  form: any = {};
  professionalId = '';
  serviceId = '';

  readonly title = this.isPros ? 'Profissionais' : 'Serviços';
  readonly subtitle = this.isPros
    ? 'Equipe que realiza os atendimentos.'
    : 'Catálogo de serviços oferecidos.';
  readonly noun = this.isPros ? 'profissional' : 'serviço';

  readonly visible = computed(() => {
    const term = this.q().trim().toLowerCase();
    if (!term) return this.items();
    return this.items().filter((x) =>
      `${x.name ?? ''} ${x.email ?? ''} ${x.description ?? ''}`.toLowerCase().includes(term),
    );
  });

  constructor() {
    this.load();
    if (this.isPros)
      this.http
        .get<any[]>(`${this.config.apiBaseUrl}/services`)
        .subscribe((x) => this.services.set(x));
  }
  startNew() {
    this.form = {};
    this.modalOpen.set(true);
  }
  edit(x: any) {
    this.form = { ...x };
    this.modalOpen.set(true);
  }
  load() {
    this.loading.set(true);
    this.http.get<any[]>(`${this.config.apiBaseUrl}/${this.kind}`).subscribe({
      next: (x) => {
        this.items.set(x);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
  save() {
    if (!this.form.name?.trim() || this.saving()) return;
    this.saving.set(true);
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
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.modalOpen.set(false);
        this.form = {};
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }
  remove() {
    const x = this.confirming();
    if (!x) return;
    this.http.delete(`${this.config.apiBaseUrl}/${this.kind}/${x.id}`).subscribe(() => {
      this.confirming.set(null);
      this.load();
    });
  }
  link() {
    if (!this.professionalId || !this.serviceId) return;
    this.http
      .put(
        `${this.config.apiBaseUrl}/professionals/${this.professionalId}/services/${this.serviceId}`,
        {},
      )
      .subscribe(() => {
        this.linkOpen.set(false);
        this.professionalId = '';
        this.serviceId = '';
        this.load();
      });
  }
}
