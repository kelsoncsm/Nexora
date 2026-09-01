# ADR-0008 — Resolução pública e contexto autenticado de tenant

## Contexto

O Nexora precisa expor páginas públicas identificáveis por empresa e, ao mesmo tempo, impedir que identificadores controlados pelo cliente se tornem autoridade de isolamento.

## Decisão

Rotas públicas tenant-scoped do MVP usam `/t/{tenantSlug}/...`. `TenantSlug` é único, normalizado e serve somente para resolução pública. Tenants inexistentes, inativos ou indisponíveis não estabelecem contexto.

Após autenticação e seleção validada de uma associação `TenantUser`, o servidor emite `tenant_id` assinado na sessão. Endpoints autenticados constroem `ITenantContext` exclusivamente dessa claim e revalidam tenant e associação ativa. `TenantId`, slug, rota, header ou DTO controlado pelo frontend jamais autorizam acesso.

A resolução é encapsulada para permitir subdomínio ou domínio customizado no futuro, sem alterar o domínio ou as regras de isolamento. Essas estratégias não fazem parte da F3.

## Alternativas

- Header `X-Tenant-Id`: simples, mas mantém um seletor técnico em todas as requisições.
- Tenant apenas na rota: legível, porém não pode ser autoridade após autenticação.
- Claim sem resolução pública: segura para APIs autenticadas, mas insuficiente para páginas públicas.
- Subdomínio/domínio customizado: boa UX futura, com complexidade operacional prematura.

## Consequências

Trocar de tenant exige reemitir a sessão após validação da associação. Slugs podem aparecer em URLs e logs, mas não concedem acesso. Mudanças de status de tenant ou vínculo têm efeito imediato porque o middleware revalida o contexto.

## Status

Accepted
