# ADR-0004 — Autenticação com JWT e refresh token rotativo

## Contexto

APIs e SPA precisam de autenticação segura, sessão de curta duração, revogação e separação clara de autorização e tenancy.

## Decisão

Usar senha com hasher seguro suportado pelo ASP.NET Core, access token JWT de curta duração e refresh token criptograficamente aleatório, revogável e rotacionado. Armazenar somente representação protegida do refresh token, validar assinatura/expiração/issuer/audience e detectar reutilização conforme a estratégia detalhada na F2.

## Alternativas

- Sessão server-side com cookie: revogação simples, mas estratégia diferente da API definida no plano.
- Access token sem refresh: implementação simples, porém UX ruim ou sessões longas.
- Provedor de identidade externo: reduz parte da operação, mas adiciona dependência e decisão comercial prematura.

## Consequências

O servidor mantém estado mínimo de refresh/revogação e precisa proteger chaves, rotação, transporte e logs. JWT não carrega autorização eterna: estado crítico e associação ao tenant continuam validados. Durações e armazenamento no cliente serão configurados e aprovados na F2.

## Status

Accepted
