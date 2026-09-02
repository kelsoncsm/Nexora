import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';

/** Toggle switch (df-switch / .nx-switch). */
@Component({
  selector: 'nx-switch',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="nx-switch">
      <input
        type="checkbox"
        [checked]="checked()"
        [disabled]="disabled()"
        [attr.aria-label]="label() || null"
        (change)="checked.set($any($event.target).checked)"
      />
      <span class="nx-switch-slider" aria-hidden="true"></span>
    </label>
  `,
})
export class NxSwitch {
  readonly checked = model(false);
  readonly disabled = input(false);
  readonly label = input('');
}
