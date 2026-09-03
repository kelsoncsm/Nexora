import { ChangeDetectionStrategy, Component, Injectable, signal } from '@angular/core';

export type NxToastTone = 'success' | 'danger' | 'info';
export interface NxToastMessage { id: number; text: string; tone: NxToastTone; }

/**
 * App-wide transient feedback. Push a message with {@link success}/{@link error}/{@link info};
 * it auto-dismisses after a few seconds. Render <nx-toast-host> once, in the shell.
 */
@Injectable({ providedIn: 'root' })
export class NxToastService {
  private seq = 0;
  readonly messages = signal<NxToastMessage[]>([]);

  success(text: string) { this.push(text, 'success'); }
  error(text: string) { this.push(text, 'danger'); }
  info(text: string) { this.push(text, 'info'); }

  dismiss(id: number) { this.messages.update((m) => m.filter((x) => x.id !== id)); }

  private push(text: string, tone: NxToastTone) {
    const id = ++this.seq;
    this.messages.update((m) => [...m, { id, text, tone }]);
    setTimeout(() => this.dismiss(id), 5000);
  }
}

@Component({
  selector: 'nx-toast-host',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="nx-toast-stack" aria-live="polite" aria-atomic="false">
      @for (t of toasts.messages(); track t.id) {
        <div class="nx-toast nx-toast--{{ t.tone }}" role="status">
          <span class="nx-toast-text">{{ t.text }}</span>
          <button type="button" class="nx-toast-close" aria-label="Fechar" (click)="toasts.dismiss(t.id)">
            <svg class="nx-ico"><use href="#i-x" /></svg>
          </button>
        </div>
      }
    </div>
  `,
})
export class NxToastHost {
  constructor(readonly toasts: NxToastService) {}
}
