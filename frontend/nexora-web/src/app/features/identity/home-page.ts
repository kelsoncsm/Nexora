import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({ selector: 'app-home-page', template: `<section class="nx-status"><span class="nx-eyebrow">Nexora</span><h1>Área protegida</h1><p>Informe o slug do tenant para abrir uma sessão operacional ou configure uma nova empresa.</p><input #slug placeholder="slug-do-tenant"><button type="button" (click)="select(slug.value)">Abrir tenant</button><button type="button" (click)="onboarding()">Criar empresa</button><button type="button" (click)="logout()">Sair</button></section>` })
export class HomePage {
  private readonly auth = inject(AuthService); private readonly router = inject(Router);
  logout(): void { this.auth.logout().subscribe(() => void this.router.navigateByUrl('/login')); }
  select(slug:string):void { this.auth.selectTenant(slug).subscribe(() => void this.router.navigateByUrl('/clientes')); }
  onboarding():void { void this.router.navigateByUrl('/onboarding'); }
}
