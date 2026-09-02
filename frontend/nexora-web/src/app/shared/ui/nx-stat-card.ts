import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type NxStatTone = 'purple' | 'green' | 'orange' | 'blue' | 'red';

/** Card de métrica/KPI (df-metric + .nx-kpi tone). */
@Component({
  selector: 'nx-stat-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': 'cls()' },
  template: `
    <div>
      <div class="nx-kpi-head">{{ label() }}</div>
      <div class="nx-kpi-value">{{ value() }}</div>
      @if (hint()) {
        <div class="nx-kpi-trend" [class.down]="down()"><ng-content></ng-content>{{ hint() }}</div>
      }
    </div>
    <span class="nx-kpi-ico"><svg class="nx-ico"><use [attr.href]="'#' + icon()" /></svg></span>
  `,
})
export class NxStatCard {
  readonly label = input.required<string>();
  readonly value = input.required<string | number>();
  readonly icon = input.required<string>();
  readonly tone = input<NxStatTone>('purple');
  readonly hint = input<string>();
  readonly down = input(false);
  readonly cls = computed(() => `nx-kpi nx-kpi--${this.tone()}`);
}
