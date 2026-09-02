import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

/** Paginação (df-pagination): "Mostrando x–y de z" + ‹ n/N ›. */
@Component({
  selector: 'nx-pagination',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (total() > 0) {
      <nav class="nx-pagination" aria-label="Paginação">
        <span class="nx-count-hint">
          Mostrando {{ start() }}–{{ end() }} de {{ total() }}
        </span>
        <span class="nx-pg-controls">
          <button
            type="button"
            class="nx-page-btn"
            [disabled]="page() <= 1"
            (click)="pageChange.emit(page() - 1)"
            aria-label="Página anterior"
          >
            <svg class="nx-ico" style="transform:rotate(180deg)"><use href="#i-chevron" /></svg>
          </button>
          <span class="nx-pg-current">{{ page() }} / {{ pages() }}</span>
          <button
            type="button"
            class="nx-page-btn"
            [disabled]="page() >= pages()"
            (click)="pageChange.emit(page() + 1)"
            aria-label="Próxima página"
          >
            <svg class="nx-ico"><use href="#i-chevron" /></svg>
          </button>
        </span>
      </nav>
    }
  `,
})
export class NxPagination {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  readonly pageChange = output<number>();

  readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize())));
  readonly start = computed(() => (this.total() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1));
  readonly end = computed(() => Math.min(this.page() * this.pageSize(), this.total()));
}
