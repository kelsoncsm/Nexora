import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { APP_CONFIG } from '../../core/config/app-config';

interface Preset {
  code: string;
  name: string;
  category: string;
  selected?: boolean;
  durationMinutes?: number;
  price?: number;
}
interface Setup {
  code: string;
  name: string;
  onboardingTitle: string;
  onboardingDescription: string;
  dashboardTitle: string;
  servicePresets: Preset[];
}

@Component({
  selector: 'app-vertical-setup-page',
  imports: [FormsModule],
  templateUrl: './vertical-setup-page.html',
  styleUrl: './vertical-setup-page.scss',
})
export class VerticalSetupPage {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  private router = inject(Router);
  readonly setup = signal<Setup | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  constructor() {
    this.http.get<Setup>(`${this.config.apiBaseUrl}/vertical-setup`).subscribe({
      next: (x) => {
        this.setup.set(x);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
  valid() {
    return (
      this.setup()
        ?.servicePresets.filter((x) => x.selected)
        .every((x) => (x.durationMinutes ?? 0) > 0 && (x.price ?? -1) >= 0) ?? true
    );
  }
  apply() {
    const model = this.setup();
    if (!model || !this.valid()) return;
    this.busy.set(true);
    this.http
      .post(`${this.config.apiBaseUrl}/vertical-setup/apply`, {
        services: model.servicePresets
          .filter((x) => x.selected)
          .map((x) => ({
            presetCode: x.code,
            durationMinutes: +x.durationMinutes!,
            price: +x.price!,
          })),
      })
      .subscribe({
        next: () => this.skip(),
        error: (e) => {
          this.busy.set(false);
          this.error.set(e.error?.title ?? 'Não foi possível aplicar a configuração.');
        },
      });
  }
  skip() {
    void this.router.navigateByUrl('/clientes');
  }
}
