import { CurrencyPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import {
  BillingInterval,
  OnboardingDraft,
  OnboardingPlan,
  OnboardingSegment,
  OnboardingService,
} from './onboarding.service';
import { NxButton } from '../../shared/ui';

@Component({
  selector: 'app-onboarding-page',
  imports: [FormsModule, CurrencyPipe, NxButton],
  templateUrl: './onboarding-page.html',
  styleUrl: './onboarding-page.scss',
})
export class OnboardingPage implements OnInit {
  private readonly service = inject(OnboardingService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly draft = signal<OnboardingDraft | null>(null);
  readonly segments = signal<OnboardingSegment[]>([]);
  readonly plans = signal<OnboardingPlan[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  ngOnInit(): void {
    forkJoin({
      draft: this.service.start(),
      segments: this.service.segments(),
      plans: this.service.plans(),
    }).subscribe({
      next: (x) => {
        if (!x.draft.timeZoneId)
          x.draft.timeZoneId = Intl.DateTimeFormat().resolvedOptions().timeZone;
        this.draft.set(x.draft);
        this.segments.set(x.segments);
        this.plans.set(x.plans);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar o onboarding.');
        this.loading.set(false);
      },
    });
  }
  choosePlan(d: OnboardingDraft, p: OnboardingPlan): void {
    d.planId = p.planId;
    d.billingInterval = p.billingInterval;
  }
  valid(d: OnboardingDraft): boolean {
    switch (d.currentStep) {
      case 1:
        return !!d.companyName?.trim() && /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(d.companySlug ?? '');
      case 2:
        return !!d.segmentId;
      case 3:
        return !!d.planId && !!d.billingInterval;
      case 4:
        return !!d.timeZoneId?.includes('/');
      default:
        return true;
    }
  }
  move(delta: number): void {
    const d = this.draft();
    if (!d) return;
    d.currentStep = Math.min(5, Math.max(1, d.currentStep + delta));
    this.save();
  }
  complete(): void {
    const d = this.draft();
    if (!d || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.service.complete(d.id).subscribe({
      next: (x) =>
        this.auth.selectTenant(x.tenantSlug).subscribe({
          next: () => void this.router.navigateByUrl('/configuracao-inicial'),
          error: () => {
            this.busy.set(false);
            this.error.set('Empresa criada, mas não foi possível abrir a sessão.');
          },
        }),
      error: (e) => {
        this.busy.set(false);
        this.error.set(e.error?.title ?? 'Não foi possível concluir o onboarding.');
      },
    });
  }
  segmentName(id: string | null): string {
    return this.segments().find((x) => x.id === id)?.name ?? '—';
  }
  planName(id: string | null, interval: BillingInterval | null): string {
    const p = this.plans().find((x) => x.planId === id && x.billingInterval === interval);
    return p ? `${p.name} · ${p.billingInterval === 'Monthly' ? 'mensal' : 'anual'}` : '—';
  }
  private save(): void {
    const d = this.draft();
    if (!d) return;
    this.busy.set(true);
    this.error.set('');
    this.service.update(d).subscribe({
      next: (x) => {
        this.draft.set(x);
        this.busy.set(false);
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(e.error?.title ?? 'Não foi possível salvar o progresso.');
      },
    });
  }
}
