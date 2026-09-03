import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NxNavGroup, NxShellPlan, NxShellUser } from './nav';

/**
 * Sidebar clara do shell (Nexora light-only, Fase B). Puramente apresentacional:
 * recebe a marca, o usuário, os grupos de navegação e o plano; emite eventos
 * de navegação (para fechar o drawer no mobile) e de logout. Cores vêm dos
 * tokens --nx-sidebar-* em styles.scss.
 */
@Component({
  selector: 'nx-sidebar',
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="nx-sidebar">
      <a class="nx-brand" routerLink="/" (click)="itemClick.emit()">
        <span class="nx-brand-mark">N</span>
        <span class="nx-brand-text"><strong>{{ brandName() }}</strong><small>{{ brandTagline() }}</small></span>
      </a>

      <div class="nx-sidebar-user">
        <span class="nx-avatar">{{ user().initials }}</span>
        <span class="nx-idnt"><b>{{ user().label }}</b><small>{{ user().role }}</small></span>
      </div>

      <nav class="nx-nav" aria-label="Navegação principal">
        @for (group of groups(); track group.label) {
          @if (group.label) {
            <p class="nx-nav-group">{{ group.label }}</p>
          }
          @for (item of group.items; track item.route) {
            <a
              [routerLink]="item.route"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: !!item.exact }"
              (click)="itemClick.emit()"
              [attr.title]="item.label"
            >
              <svg class="nx-ico"><use [attr.href]="'#' + item.icon" /></svg>
              <span>{{ item.label }}</span>
            </a>
          }
        }
      </nav>

      @if (plan(); as p) {
        <div class="nx-plan">
          <span class="nx-plan-top"><svg class="nx-ico"><use href="#i-crown" /></svg>Plano {{ p.code }}</span>
          <span class="nx-badge" [class.nx-badge--success]="p.active" [class.nx-badge--warning]="!p.active">{{ p.statusLabel }}</span>
          <small>{{ p.tenantLabel }}</small>
          <a class="nx-plan-cta" routerLink="/assinatura" (click)="itemClick.emit()">Gerenciar plano</a>
        </div>
      }

      <div class="nx-sidebar-foot">
        <a routerLink="/perfil" (click)="itemClick.emit()">
          <svg class="nx-ico"><use href="#i-user" /></svg><span>Meu perfil</span>
        </a>
        <button type="button" class="nx-logout" (click)="logout.emit()">
          <svg class="nx-ico"><use href="#i-logout" /></svg><span>Sair</span>
        </button>
      </div>
    </aside>
  `,
})
export class NxSidebar {
  readonly brandName = input('Nexora');
  readonly brandTagline = input('Plataforma MultiNicho');
  readonly user = input.required<NxShellUser>();
  readonly groups = input.required<NxNavGroup[]>();
  readonly plan = input<NxShellPlan | null>(null);
  readonly itemClick = output<void>();
  readonly logout = output<void>();
}
