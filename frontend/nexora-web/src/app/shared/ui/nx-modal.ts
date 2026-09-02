import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  input,
  output,
  viewChild,
} from '@angular/core';

/**
 * Modal acessível (padrão df-modal): move o foco para o diálogo ao abrir,
 * prende o foco com Tab/Shift+Tab, fecha com ESC e devolve o foco ao abrir.
 * Renderize condicionalmente com `@if`. Projeção: conteúdo padrão vai no corpo;
 * `[footer]` vai no rodapé.
 */
@Component({
  selector: 'nx-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="nx-modal-overlay" (click)="close.emit()">
      <div
        #dialog
        class="nx-modal-box"
        [class.nx-modal-box--sm]="small()"
        role="dialog"
        aria-modal="true"
        tabindex="-1"
        (click)="$event.stopPropagation()"
      >
        <div class="nx-modal-header">
          <h3>{{ title() }}</h3>
          <button type="button" class="nx-icon-btn" (click)="close.emit()" aria-label="Fechar">
            <svg class="nx-ico"><use href="#i-x" /></svg>
          </button>
        </div>
        <div class="nx-modal-body"><ng-content></ng-content></div>
        <div class="nx-modal-footer"><ng-content select="[footer]"></ng-content></div>
      </div>
    </div>
  `,
})
export class NxModal implements AfterViewInit, OnDestroy {
  readonly title = input.required<string>();
  readonly small = input(false);
  readonly close = output<void>();

  private readonly dialog = viewChild<ElementRef<HTMLElement>>('dialog');
  private previous: HTMLElement | null = null;

  private static readonly FOCUSABLE =
    'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';

  ngAfterViewInit(): void {
    this.previous = document.activeElement as HTMLElement | null;
    this.dialog()?.nativeElement.focus();
  }

  ngOnDestroy(): void {
    this.previous?.focus?.();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close.emit();
  }

  @HostListener('document:keydown.tab', ['$event'])
  onTab(event: Event): void {
    this.trap(event, false);
  }

  @HostListener('document:keydown.shift.tab', ['$event'])
  onShiftTab(event: Event): void {
    this.trap(event, true);
  }

  private trap(event: Event, back: boolean): void {
    const box = this.dialog()?.nativeElement;
    if (!box) return;
    const focusable = Array.from(box.querySelectorAll<HTMLElement>(NxModal.FOCUSABLE)).filter(
      (el) => el.getClientRects().length > 0,
    );
    if (focusable.length === 0) {
      event.preventDefault();
      box.focus();
      return;
    }
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement as HTMLElement | null;
    if (!active || !box.contains(active) || active === box) {
      event.preventDefault();
      (back ? last : first).focus();
      return;
    }
    if (back && active === first) {
      event.preventDefault();
      last.focus();
    } else if (!back && active === last) {
      event.preventDefault();
      first.focus();
    }
  }
}
