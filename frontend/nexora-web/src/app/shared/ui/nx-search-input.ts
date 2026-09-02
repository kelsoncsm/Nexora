import { ChangeDetectionStrategy, Component, input, model, output } from '@angular/core';
import { Subject, debounceTime } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

/** Campo de busca com ícone (.nx-list-search). Emite `search` com debounce. */
@Component({
  selector: 'nx-search-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="nx-list-search">
      <svg class="nx-ico"><use href="#i-search" /></svg>
      <input
        type="search"
        [value]="value()"
        [placeholder]="placeholder()"
        [attr.aria-label]="placeholder()"
        (input)="onInput($any($event.target).value)"
        (keyup.enter)="search.emit(value())"
      />
    </label>
  `,
})
export class NxSearchInput {
  readonly value = model('');
  readonly placeholder = input('Buscar…');
  readonly debounce = input(300);
  readonly search = output<string>();

  private readonly typed$ = new Subject<string>();

  constructor() {
    this.typed$
      .pipe(debounceTime(this.debounce()), takeUntilDestroyed())
      .subscribe((v) => this.search.emit(v));
  }

  onInput(v: string): void {
    this.value.set(v);
    this.typed$.next(v);
  }
}
