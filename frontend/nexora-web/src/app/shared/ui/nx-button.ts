import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type NxButtonVariant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger';
export type NxButtonSize = 'sm' | 'md';

/**
 * Botão do design system (df-button). Renderiza um `<button>` nativo — use
 * `routerLink` no host quando precisar navegar (Angular liga o RouterLink ao
 * elemento hospedeiro). Ícone via id do sprite.
 */
@Component({
  selector: 'button[nx-button], a[nx-button]',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class]': 'cls()',
    '[attr.aria-busy]': 'loading() || null',
    '[attr.disabled]': "isDisabled() ? '' : null",
    '[attr.aria-disabled]': 'isDisabled() || null',
  },
  template: `
    @if (icon() && !loading()) {
      <svg class="nx-ico"><use [attr.href]="'#' + icon()" /></svg>
    }
    @if (loading()) {
      <span class="nx-btn-spinner" aria-hidden="true"></span>
    }
    <ng-content></ng-content>
  `,
})
export class NxButton {
  readonly variant = input<NxButtonVariant>('primary');
  readonly size = input<NxButtonSize>('md');
  readonly icon = input<string>();
  readonly loading = input(false);
  readonly disabled = input(false);
  readonly block = input(false);

  readonly isDisabled = computed(() => this.disabled() || this.loading());
  readonly cls = computed(() => {
    const parts = ['nx-btn'];
    if (this.variant() !== 'primary') parts.push(`nx-btn--${this.variant()}`);
    if (this.size() === 'sm') parts.push('nx-btn--sm');
    if (this.block()) parts.push('nx-btn--block');
    return parts.join(' ');
  });
}
