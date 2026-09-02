import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Campo de formulário: label + controle projetado + dica/erro (df-field). */
@Component({
  selector: 'nx-form-field',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'nx-form-group' },
  template: `
    @if (label()) {
      <label class="nx-form-label" [attr.for]="for()">{{ label() }}</label>
    }
    <ng-content></ng-content>
    @if (error()) {
      <p class="nx-form-error">{{ error() }}</p>
    } @else if (hint()) {
      <p class="nx-form-hint">{{ hint() }}</p>
    }
  `,
})
export class NxFormField {
  readonly label = input<string>();
  readonly for = input<string>();
  readonly hint = input<string>();
  readonly error = input<string>();
}
