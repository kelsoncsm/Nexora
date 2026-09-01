import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Button } from 'primeng/button';

@Component({
  selector: 'nx-button',
  imports: [Button],
  template: `<p-button [label]="label()" (onClick)="pressed.emit()" />`,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NxButton {
  readonly label = input.required<string>();
  readonly pressed = output<void>();
}
