import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { InvitationAcceptService } from './invitation-accept.service';
import { NxToastService } from '../../shared/ui';

@Component({
  selector: 'app-invitation-accept-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './invitation-accept-page.html',
})
export class InvitationAcceptPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(InvitationAcceptService);
  private readonly toasts = inject(NxToastService);

  readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';
  readonly busy = signal(false);
  readonly error = signal('');
  readonly done = signal<{ tenantName: string } | null>(null);

  readonly form = new FormGroup(
    {
      password: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.minLength(12)],
      }),
      passwordConfirmation: new FormControl('', { nonNullable: true }),
    },
    { validators: (g) => (g.get('password')!.value === g.get('passwordConfirmation')!.value ? null : { mismatch: true }) },
  );

  submit(): void {
    if (!this.token) {
      this.error.set('Link de convite inválido.');
      return;
    }
    if (this.form.invalid) {
      this.error.set(
        this.form.errors?.['mismatch']
          ? 'A confirmação de senha não confere.'
          : 'Use uma senha com pelo menos 12 caracteres.',
      );
      return;
    }
    this.busy.set(true);
    this.error.set('');
    const { password, passwordConfirmation } = this.form.getRawValue();
    this.service.accept(this.token, password, passwordConfirmation).subscribe({
      next: (result) => {
        this.busy.set(false);
        this.done.set({ tenantName: result.tenantName });
        this.toasts.success(`Convite aceito. Você agora faz parte de ${result.tenantName}.`);
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(
          e?.status === 401
            ? 'Senha incorreta para a conta deste e-mail.'
            : e?.status === 400
              ? (e?.error?.detail ?? e?.error?.title ?? 'Este convite é inválido ou expirou.')
              : 'Não foi possível aceitar o convite. Tente novamente.',
        );
      },
    });
  }

  goToLogin(): void {
    void this.router.navigateByUrl('/login');
  }
}
