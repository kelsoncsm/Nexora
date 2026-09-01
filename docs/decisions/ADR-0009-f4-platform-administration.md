# ADR-0009 — Escopo de Platform Administration

- Status: Accepted
- Data: 2026-09-01

## Contexto

A administração global precisa gerenciar tenants, usuários, segmentos e auditoria sem transformar Tenant Admin em administrador da plataforma nem contornar o isolamento tenant-scoped.

## Decisão

O papel global `PlatformAdmin` usa permissões explícitas `platform.*` e policy exclusiva nos endpoints `/api/v1/admin/...`. A policy rejeita sessões com claim `tenant_id`, mantendo Platform Scope e Tenant Scope separados. O primeiro administrador é promovido de forma idempotente por `Administration:BootstrapAdminEmail`, somente para usuário já cadastrado; quando ausente, o bootstrap fica desabilitado. Não existe API pública de promoção.

`BusinessSegment` é catálogo global. `AuditLog` é append-only e registra ator, ação, tipo e identificador do alvo, resultado, correlação e instante.

## Consequências

- Tenant Admin e usuário comum recebem `403` nos endpoints de plataforma.
- Operações administrativas críticas geram auditoria estruturada.
- A configuração de bootstrap deve ser protegida operacionalmente e removida após o provisionamento quando apropriado.
- Platform Admin não ganha acesso implícito aos dados internos de tenants.
