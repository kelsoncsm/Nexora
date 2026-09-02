import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Estado vazio (df-empty-state): ícone + título + descrição + slot de ação. */
@Component({
  selector: 'nx-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="nx-empty" role="status">
      <svg class="nx-ico"><use [attr.href]="'#' + icon()" /></svg>
      <b>{{ title() }}</b>
      @if (description()) {
        <span>{{ description() }}</span>
      }
      <ng-content></ng-content>
    </div>
  `,
})
export class NxEmptyState {
  readonly icon = input('i-search');
  readonly title = input.required<string>();
  readonly description = input<string>();
}
