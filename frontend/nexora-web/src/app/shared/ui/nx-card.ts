import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Card branco padrão (df-card). `flat` = borda em vez de sombra; `title` opcional com ícone. */
@Component({
  selector: 'nx-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.nx-card]': '!flat()', '[class.nx-card--flat]': 'flat()' },
  template: `
    @if (heading()) {
      <div class="nx-card-title">
        @if (icon()) {
          <svg class="nx-ico"><use [attr.href]="'#' + icon()" /></svg>
        }
        {{ heading() }}
      </div>
    }
    <ng-content></ng-content>
  `,
})
export class NxCard {
  readonly flat = input(false);
  readonly heading = input<string>();
  readonly icon = input<string>();
}
