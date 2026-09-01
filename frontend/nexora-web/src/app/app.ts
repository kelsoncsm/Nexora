import { Component, computed, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './core/auth/auth.service';
import { HttpClient } from '@angular/common/http';
import { APP_CONFIG } from './core/config/app-config';

interface ShellPlan{planCode:string;status:string}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet,RouterLink,RouterLinkActive],
  template: `
    @if (showShell()) {
      <div class="nx-app" [class.nav-open]="navOpen()" [class.collapsed]="collapsed()">
        <aside class="nx-sidebar">
          <a class="nx-logo" routerLink="/"><span>N</span><strong>Nexora</strong></a>
          <nav aria-label="Navegação principal">
            <small>ESPAÇO DE TRABALHO</small>
            <a routerLink="/agenda" routerLinkActive="active"><i>◫</i>Agenda</a>
            @if(has('customers.read')){<a routerLink="/clientes" routerLinkActive="active"><i>♧</i>Clientes</a>}
            @if(has('professionals.read')){<a routerLink="/profissionais" routerLinkActive="active"><i>◇</i>Profissionais</a>}
            @if(has('services.read')){<a routerLink="/servicos" routerLinkActive="active"><i>⌁</i>Serviços</a>}
            <a routerLink="/assinatura" routerLinkActive="active"><i>◉</i>Assinatura</a>
            @if(auth.isPlatformAdmin()){
              <small>PLATAFORMA</small><a routerLink="/admin" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}"><i>▦</i>Administração</a><a routerLink="/admin/assinaturas" routerLinkActive="active"><i>↗</i>Assinaturas</a><a routerLink="/admin/billing" routerLinkActive="active"><i>◆</i>Billing</a>
            }
          </nav>
          @if(plan()){<a class="nx-plan" routerLink="/assinatura"><small>{{plan()!.planCode}}</small><b>{{plan()!.status}}</b><span>Gerenciar plano →</span></a>}
          <div class="nx-sidebar-foot"><span class="nx-avatar">NX</span><div><b>{{auth.userLabel()}}</b><small>{{tenantLabel()}}</small></div></div>
        </aside>
        <button class="nx-scrim" aria-label="Fechar menu" (click)="navOpen.set(false)"></button>
        <section class="nx-workspace">
          <header class="nx-topbar"><button class="nx-menu" (click)="toggleNav()" aria-label="Abrir ou recolher menu">☰</button><label class="nx-search"><span>⌕</span><input aria-label="Busca rápida" placeholder="Buscar no Nexora..."></label><div class="nx-breadcrumb"><span>Nexora</span><b>{{pageTitle()}}</b></div><div class="nx-top-actions"><button title="Ajuda" aria-label="Ajuda">?</button><button title="Notificações" aria-label="Notificações">⌁</button><span class="nx-live"><i></i>Online</span></div></header>
          <main class="nx-content"><router-outlet /></main>
        </section>
      </div>
    } @else { <main class="nx-public"><router-outlet /></main> }
  `
})
export class App {readonly auth=inject(AuthService);private router=inject(Router);private http=inject(HttpClient);private cfg=inject(APP_CONFIG);readonly navOpen=signal(false);readonly collapsed=signal(false);readonly plan=signal<ShellPlan|null>(null);readonly currentUrl=signal(this.router.url);readonly showShell=computed(()=>this.auth.isAuthenticated()&&!['/login','/cadastro'].includes(this.currentUrl().split('?')[0]));readonly tenantLabel=computed(()=>this.auth.tenantId()?`Tenant ${this.auth.tenantId()!.slice(0,8)}`:'Escopo da plataforma');readonly pageTitle=computed(()=>{const p=this.currentUrl();return p.includes('agenda')?'Agenda':p.includes('clientes')?'Clientes':p.includes('profissionais')?'Profissionais':p.includes('servicos')?'Serviços':p.includes('billing')?'Billing':p.includes('assinatura')?'Assinatura':p.includes('admin')?'Administração':'Visão geral'});constructor(){this.router.events.pipe(filter((e):e is NavigationEnd=>e instanceof NavigationEnd)).subscribe(e=>{this.currentUrl.set(e.urlAfterRedirects);this.navOpen.set(false)});effect(()=>{if(this.auth.tenantId())this.http.get<ShellPlan>(`${this.cfg.apiBaseUrl}/subscription`).subscribe({next:x=>this.plan.set(x),error:()=>this.plan.set(null)});else this.plan.set(null)})}has(permission:string){return this.auth.permissions().includes(permission)}toggleNav(){if(window.innerWidth<=900)this.navOpen.set(!this.navOpen());else this.collapsed.set(!this.collapsed())}}
