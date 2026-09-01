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
    const request =
      this.mode() === 'login'
        ? this.auth.login(this.form.getRawValue())
        : this.auth.register(this.form.getRawValue());
    request.subscribe({
      next: () => void this.router.navigateByUrl(this.mode() === 'register' ? '/onboarding' : '/'),
      error: () => {
        this.busy.set(false);
        this.error.set(true);
      },
    });
  }
}
