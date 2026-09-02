import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
// Nexora usa apenas tema claro — não há alternância de tema no shell.
import { RouterLink } from '@angular/router';
import { NxShellUser } from './nav';

/** Topbar do shell (padrão DentalFlow): hambúrguer + título + busca + ações + menu do usuário. */
@Component({
  selector: 'nx-topbar',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="nx-topbar">
      <div class="nx-topbar-left">
        <button class="nx-menu-btn" (click)="toggleNav.emit()" aria-label="Abrir ou recolher menu">
          <svg class="nx-ico"><use href="#i-menu" /></svg>
        </button>
        <h1 class="nx-topbar-title">{{ title() }}</h1>
      </div>
      <div class="nx-topbar-right">
        <label class="nx-search">
          <svg class="nx-ico"><use href="#i-search" /></svg>
          <input aria-label="Busca rápida" placeholder="Buscar…" />
          <kbd>Ctrl K</kbd>
        </label>
        <button class="nx-iconbtn" title="Notificações" aria-label="Notificações">
          <svg class="nx-ico"><use href="#i-bell" /></svg><span class="nx-dot"></span>
        </button>
        <div class="nx-dropdown">
          <button
            class="nx-user-chip"
            (click)="open.set(!open())"
            [attr.aria-expanded]="open()"
            aria-label="Menu da conta"
          >
            <span class="nx-avatar">{{ user().initials }}</span>
            <span class="nx-idnt"><b>{{ user().label }}</b><small>{{ user().role }}</small></span>
            <svg class="nx-ico"><use href="#i-chevron-d" /></svg>
          </button>
          @if (open()) {
            <div class="nx-dropdown-menu">
              <a routerLink="/perfil" (click)="open.set(false)"><svg class="nx-ico"><use href="#i-user" /></svg>Meu perfil</a>
              <a routerLink="/assinatura" (click)="open.set(false)"><svg class="nx-ico"><use href="#i-card" /></svg>Minha assinatura</a>
              <button type="button" class="danger" (click)="open.set(false); logout.emit()">
                <svg class="nx-ico"><use href="#i-logout" /></svg>Sair
              </button>
            </div>
          }
        </div>
      </div>
    </header>
  `,
})
export class NxTopbar {
  readonly title = input.required<string>();
  readonly user = input.required<NxShellUser>();
  readonly toggleNav = output<void>();
  readonly logout = output<void>();
  readonly open = signal(false);
}
