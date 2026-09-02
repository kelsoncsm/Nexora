import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';

export interface NxChipOption {
  value: string;
  label: string;
}

/** Filtros em pílula (df-status-filter / .nx-chip-filter). */
@Component({
  selector: 'nx-chip-filter',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'nx-chip-filter', role: 'group' },
  template: `
    @for (opt of options(); track opt.value) {
      <button type="button" [class.active]="value() === opt.value" (click)="value.set(opt.value)">
        {{ opt.label }}
      </button>
    }
  `,
})
export class NxChipFilter {
  readonly options = input.required<NxChipOption[]>();
  readonly value = model<string>('');
}
