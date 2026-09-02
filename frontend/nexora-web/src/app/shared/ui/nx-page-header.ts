import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Cabeçalho de página: título + descrição + slot de ações (padrão df-page-header). */
@Component({
  selector: 'nx-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="nx-page-header">
      <div>
        <h1>{{ title() }}</h1>
        @if (subtitle()) {
          <p>{{ subtitle() }}</p>
        }
      </div>
      <div class="nx-ph-actions"><ng-content></ng-content></div>
    </div>
  `,
})
export class NxPageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
}
