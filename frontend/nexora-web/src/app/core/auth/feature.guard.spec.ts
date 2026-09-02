import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { firstValueFrom, isObservable, of } from 'rxjs';
import { routes } from '../../app.routes';
import { featureGuard } from './feature.guard';
import { AuthService } from './auth.service';

describe('featureGuard', () => {
  function run(auth: unknown) {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), { provide: AuthService, useValue: auth }],
    });
    return TestBed.runInInjectionContext(() => featureGuard('REPORTS')({} as never, {} as never));
  }

  it('allows the route when the plan includes the module', async () => {
    const result = run({ tenantId: () => 't1', loadFeatures: () => of({ REPORTS: { enabled: true, limit: null } }) });
    expect(isObservable(result)).toBe(true);
    expect(await firstValueFrom(result as never)).toBe(true);
  });

  it('redirects to /403 when the module is not in the plan', async () => {
    const result = run({ tenantId: () => 't1', loadFeatures: () => of({ REPORTS: { enabled: false, limit: null } }) });
    const value = (await firstValueFrom(result as never)) as UrlTree;
    expect(value instanceof UrlTree).toBe(true);
    expect(TestBed.inject(Router).serializeUrl(value)).toBe('/403');
  });

  it('passes through when there is no tenant session yet (tenantGuard owns that)', () => {
    expect(run({ tenantId: () => null })).toBe(true);
  });
});
