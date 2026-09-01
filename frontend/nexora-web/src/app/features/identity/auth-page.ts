import { Component, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-auth-page', imports: [ReactiveFormsModule, RouterLink],
  template: `<section class="nx-auth" aria-labelledby="auth-title"><span class="nx-eyebrow">Nexora</span>
    <h1 id="auth-title">{{ mode() === 'login' ? 'Entrar' : 'Criar conta' }}</h1>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <label>E-mail<input type="email" formControlName="email" autocomplete="email" /></label>
      <label>Senha<input type="password" formControlName="password" [autocomplete]="mode() === 'login' ? 'current-password' : 'new-password'" /></label>
      @if (error()) { <p class="nx-error" role="alert">Não foi possível autenticar. Verifique os dados.</p> }
      <button type="submit" [disabled]="form.invalid || busy()">{{ busy() ? 'Aguarde…' : 'Continuar' }}</button>
    </form><a [routerLink]="mode() === 'login' ? '/cadastro' : '/login'">{{ mode() === 'login' ? 'Criar uma conta' : 'Já tenho uma conta' }}</a></section>`
})
export class AuthPage {
  readonly mode = input.required<'login' | 'register'>();
  private readonly auth = inject(AuthService); private readonly router = inject(Router);
  readonly busy = signal(false); readonly error = signal(false);
  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(12)] })
  });
  submit(): void {
    if (this.form.invalid) return;
    this.busy.set(true); this.error.set(false);
    const request = this.mode() === 'login' ? this.auth.login(this.form.getRawValue()) : this.auth.register(this.form.getRawValue());
    request.subscribe({ next: () => void this.router.navigateByUrl(this.mode() === 'register' ? '/onboarding' : '/'), error: () => { this.busy.set(false); this.error.set(true); } });
  }
}
