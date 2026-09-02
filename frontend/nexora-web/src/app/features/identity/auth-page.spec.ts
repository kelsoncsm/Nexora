import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthPage } from './auth-page';
import { AuthService, UserTenant } from '../../core/auth/auth.service';

/**
 * P1.2 — after a successful login the user re-enters their company without typing a slug:
 * a single membership is auto-selected, multiple memberships open the picker, none lands on home.
 */
describe('AuthPage — tenant re-entry after login', () => {
  const ALPHA: UserTenant = { id: 't1', name: 'Alpha', slug: 'alpha-co', roleName: 'ADMIN' };
  const BRAVO: UserTenant = { id: 't2', name: 'Bravo', slug: 'bravo-co', roleName: 'ADMIN' };

  async function setup(tenants: UserTenant[]) {
    const selectTenant = vi.fn((_: string) => of(undefined));
    const auth: Partial<AuthService> = {
      login: () => of(undefined),
      register: () => of(undefined),
      listMyTenants: () => of(tenants),
      selectTenant,
    };
    TestBed.configureTestingModule({
      imports: [AuthPage],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    await TestBed.compileComponents();
    const fixture = TestBed.createComponent(AuthPage);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    fixture.componentRef.setInput('mode', 'login');
    fixture.componentInstance.form.setValue({ email: 'user@nexora.test', password: 'Correct-Horse-42' });
    return { fixture, navigate, selectTenant };
  }

  it('auto-selects the only company and lands on home', async () => {
    const { fixture, navigate, selectTenant } = await setup([ALPHA]);
    fixture.componentInstance.submit();
    await fixture.whenStable();
    expect(selectTenant).toHaveBeenCalledWith('alpha-co');
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('opens the company picker when the user has more than one company', async () => {
    const { fixture, navigate, selectTenant } = await setup([ALPHA, BRAVO]);
    fixture.componentInstance.submit();
    await fixture.whenStable();
    expect(selectTenant).not.toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/selecionar-empresa');
  });

  it('sends a user with no company to the home screen', async () => {
    const { fixture, navigate, selectTenant } = await setup([]);
    fixture.componentInstance.submit();
    await fixture.whenStable();
    expect(selectTenant).not.toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/');
  });
});
