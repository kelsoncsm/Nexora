import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NxModal } from './nx-modal';

/** Diálogo de confirmação (df-confirm). Renderize com `@if`. */
@Component({
  selector: 'nx-confirm-dialog',
  imports: [NxModal],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nx-modal [title]="title()" [small]="true" (close)="cancel.emit()">
      <p class="nx-confirm-text"><ng-content></ng-content></p>
      <div footer>
        <button type="button" class="nx-btn nx-btn--secondary" (click)="cancel.emit()">
          {{ cancelLabel() }}
        </button>
        <button
          type="button"
          class="nx-btn"
          [class.nx-btn--danger]="danger()"
          (click)="confirm.emit()"
        >
          {{ confirmLabel() }}
        </button>
      </div>
    </nx-modal>
  `,
})
export class NxConfirmDialog {
  readonly title = input.required<string>();
  readonly confirmLabel = input('Confirmar');
  readonly cancelLabel = input('Cancelar');
  readonly danger = input(false);
  readonly confirm = output<void>();
  readonly cancel = output<void>();
}
