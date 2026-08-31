# ADR-0002 — Multi-tenancy com banco e tabelas compartilhados

## Contexto

O SaaS precisa isolar empresas com eficiência operacional desde o início e impedir leitura, alteração, exclusão ou associação cross-tenant.

## Decisão

Usar inicialmente PostgreSQL compartilhado, tabelas compartilhadas e `TenantId` obrigatório nos dados tenant-scoped. O backend resolve e valida o tenant pelo contexto autenticado; valores enviados pelo cliente não são autoridade. Queries, comandos, relacionamentos e testes aplicam isolamento.

## Alternativas

- Schema por tenant: isolamento adicional, com maior complexidade de migrations e operação.
- Banco por tenant: isolamento forte, com custo operacional e provisionamento maiores.
- Modelo compartilhado escolhido: operação simples e eficiente, exigindo defesa em profundidade rigorosa.

## Consequências

Consultas e índices devem considerar `TenantId`; unicidade tende a ser composta; bypasses são explícitos e auditados. Testes de isolamento e IDOR são obrigatórios para toda entidade tenant-scoped. Uma mudança de estratégia requer migração e novo ADR.

## Status

Accepted
