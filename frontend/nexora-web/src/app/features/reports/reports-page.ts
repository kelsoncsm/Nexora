import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { APP_CONFIG } from '../../core/config/app-config';
import { NxAvatar, NxButton, NxCard, NxDataTable, NxFormField, NxPageHeader, NxStatCard } from '../../shared/ui';

interface ReportData {
  totalTenants?: number;
  activeTenants?: number;
  trialSubscriptions?: number;
  activeSubscriptions?: number;
  pastDueSubscriptions?: number;
  customers?: number;
  appointments?: { total: number; completed: number; cancelled: number; noShow: number };
  paidRevenue?: number;
  currency?: string;
  productivity?: { professionalName: string; completedAppointments: number }[];
}

@Component({
  selector: 'app-reports-page',
  imports: [FormsModule, NxPageHeader, NxCard, NxFormField, NxButton, NxStatCard, NxDataTable, NxAvatar],
  templateUrl: './reports-page.html',
  styleUrl: './reports-page.scss',
})
export class ReportsPage {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  platform = inject(ActivatedRoute).snapshot.data['platform'] === true;
  from = new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10);
  to = new Date(Date.now() + 86400000).toISOString().slice(0, 10);
  data = signal<ReportData | null>(null);
  error = signal('');
  constructor() {
    this.load();
  }
  load() {
    const path = this.platform ? '/admin/reports/overview' : '/reports/overview';
    this.http
      .get<ReportData>(
        `${this.config.apiBaseUrl}${path}?from=${this.from}T00:00:00Z&to=${this.to}T00:00:00Z`,
      )
      .subscribe({
        next: (x) => {
          this.data.set(x);
          this.error.set('');
        },
        error: () => this.error.set('Não foi possível carregar os indicadores.'),
      });
  }
}
