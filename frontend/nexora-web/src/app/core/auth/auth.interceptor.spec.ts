import { isNexoraApiRequest } from './auth.interceptor';

describe('authInterceptor destination policy', () => {
  const origin = 'https://app.nexora.test';

  it('accepts only the configured Nexora API boundary', () => {
    expect(isNexoraApiRequest('/api/v1/customers', '/api/v1', origin)).toBe(true);
    expect(isNexoraApiRequest('https://app.nexora.test/api/v1/reports', '/api/v1', origin)).toBe(true);
    expect(isNexoraApiRequest('/api/v10/escape', '/api/v1', origin)).toBe(false);
  });

  it('rejects arbitrary external destinations', () => {
    expect(isNexoraApiRequest('https://api.resend.com/emails', '/api/v1', origin)).toBe(false);
    expect(isNexoraApiRequest('https://api.mercadopago.com/v1/payments', '/api/v1', origin)).toBe(false);
    expect(isNexoraApiRequest('https://cdn.example.test/api/v1/file', '/api/v1', origin)).toBe(false);
  });
});
