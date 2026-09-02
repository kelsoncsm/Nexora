import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Component } from '@angular/core';
import { NxTopbar } from './nx-topbar';

@Component({
  standalone: true,
  imports: [NxTopbar],
  template: `<nx-topbar title="Visão Geral" [user]="user" />`,
})
class HostComponent {
  readonly user = { label: 'u@nexora.test', role: 'Operação', initials: 'UN' };
}

describe('NxTopbar', () => {
  function render() {
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideRouter([])],
    });
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renderiza o título e o menu da conta', () => {
    const el = render().nativeElement as HTMLElement;
    expect(el.querySelector('.nx-topbar-title')?.textContent).toContain('Visão Geral');
    expect(el.querySelector('.nx-user-chip')).toBeTruthy();
  });

  it('não tem controle de alternância de tema, nem fechado nem no dropdown', () => {
    const fixture = render();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[aria-label="Alternar tema"]')).toBeNull();
    expect(el.innerHTML).not.toContain('#i-sun');
    expect(el.innerHTML).not.toContain('#i-moon');

    (el.querySelector('.nx-user-chip') as HTMLButtonElement).click();
    fixture.detectChanges();
    const menu = el.querySelector('.nx-dropdown-menu');
    expect(menu).toBeTruthy();
    expect(menu!.textContent).not.toMatch(/[Tt]ema/);
  });
});
