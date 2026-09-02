import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type NxBadgeTone = 'success' | 'danger' | 'warning' | 'info' | 'neutral';

/** Badge de status (df-badge): cor + fundo suave, pill. */
@Component({
  selector: 'nx-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': 'cls()' },
  template: `<ng-content></ng-content>`,
})
export class NxBadge {
  readonly tone = input<NxBadgeTone>('neutral');
  readonly cls = computed(() => `nx-badge nx-badge--${this.tone()}`);
}
