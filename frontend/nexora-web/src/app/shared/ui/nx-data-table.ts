import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NxEmptyState } from './nx-empty-state';

/**
 * Contêiner de listagem (df-table-wrapper): card + área rolável + skeleton de
 * carregamento + estado vazio. A `<table class="nx-table">` (com `thead`/`tbody`)
 * é projetada; o componente decide se mostra a tabela, o skeleton ou o vazio.
 * Rodapé opcional (paginação) via `[footer]`.
 */
@Component({
  selector: 'nx-data-table',
  imports: [NxEmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="nx-card">
      @if (loading()) {
        <div class="nx-table-skeleton">
          @for (i of skeletonRows; track i) {
            <div class="nx-skeleton" style="height: 46px"></div>
          }
        </div>
      } @else if (empty()) {
        <nx-empty-state [icon]="emptyIcon()" [title]="emptyTitle()" [description]="emptyDescription()">
          <ng-content select="[emptyAction]"></ng-content>
        </nx-empty-state>
      } @else {
        <div class="nx-table-wrap"><ng-content></ng-content></div>
      }
      <ng-content select="[footer]"></ng-content>
    </div>
  `,
})
export class NxDataTable {
  readonly loading = input(false);
  readonly empty = input(false);
  readonly emptyIcon = input('i-search');
  readonly emptyTitle = input('Nada encontrado');
  readonly emptyDescription = input<string>();
  readonly skeletonRows = [1, 2, 3, 4, 5];
}
