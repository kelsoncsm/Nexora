import { Component, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-auth-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './auth-page.html',
})
export class AuthPage {
  readonly mode = input.required<'login' | 'register'>();
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly busy = signal(false);
  readonly error = signal(false);
  readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(12)],
    }),
  });
  submit(): void {
    if (this.form.invalid) return;
    this.busy.set(true);
    this.error.set(false);
    if (this.mode() === 'register') {
      this.auth.register(this.form.getRawValue()).subscribe({
        next: () => void this.router.navigateByUrl('/onboarding'),
        error: () => this.fail(),
      });
      return;
    }
    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => this.enterAfterLogin(),
      error: () => this.fail(),
    });
  }

  /**
   * After a successful login, resolve where the user lands: their only company (auto-selected),
   * a company picker for multiple memberships, or the home screen when they have none. The tenant
   * is always selected server-side against the user's real memberships — the slug is never trusted.
   */
  private enterAfterLogin(): void {
    this.auth.listMyTenants().subscribe({
      next: (tenants) => {
        if (tenants.length === 1) {
          this.auth.selectTenant(tenants[0].slug).subscribe({
            next: () => void this.router.navigateByUrl('/'),
            error: () => void this.router.navigateByUrl('/'),
          });
        } else if (tenants.length > 1) {
          void this.router.navigateByUrl('/selecionar-empresa');
        } else {
          void this.router.navigateByUrl('/');
        }
      },
      error: () => void this.router.navigateByUrl('/'),
    });
  }

  private fail(): void {
    this.busy.set(false);
    this.error.set(true);
  }
}
