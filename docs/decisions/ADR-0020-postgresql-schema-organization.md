# ADR-0020 — Organização do PostgreSQL por schemas de contexto

## Status

Aceito — 2026-09-02. Refatoração de persistência, sem mudança funcional.

## Contexto

Todas as 31 tabelas de domínio viviam em `public`. A camada de persistência já é
organizada por contexto (11 `IEntityTypeConfiguration` agrupados por módulo) e a direção de
dependência é travada por `DependencyDirectionTests`, mas o banco não reflete essa divisão:
`\dt` num `psql` mostra uma lista plana, sem pista de qual módulo é dono de quê.

Esta é uma reorganização física. Não altera regra de negócio, API, autenticação,
autorização/RBAC, feature enforcement, Subscription/Billing nem a estratégia de multi-tenancy.

## Decisão

### 1. Um schema PostgreSQL por bounded context

| Schema | Responsabilidade | Tabelas |
|---|---|---|
| `identity` | Autenticação, usuários, roles globais, refresh tokens | `users`, `roles`, `permissions`, `role_permissions`, `user_roles`, `refresh_tokens` |
| `tenancy` | Tenants, associação usuário↔tenant, RBAC tenant-scoped | `tenants`, `tenant_users`, `tenant_roles`, `tenant_role_permissions` |
| `platform` | Administração da plataforma (escopo global, não tenant) | `business_segments`, `audit_logs` |
| `plans` | Catálogo de planos/features e overrides por tenant | `features`, `plans`, `plan_features`, `tenant_feature_overrides` |
| `billing` | Assinaturas, faturas, pagamentos, webhooks processados | `subscriptions`, `subscription_events`, `plan_prices`, `billing_invoices`, `billing_payments`, `processed_webhook_events` |
| `customers` | Clientes do tenant | `customers` |
| `catalog` | Profissionais, serviços e vínculos | `professionals`, `services`, `professional_services` |
| `scheduling` | Agenda: horários, bloqueios, agendamentos | `working_hours`, `blocked_periods`, `appointments` |
| `notifications` | Outbox de e-mail transacional | `email_outbox_messages` |
| `onboarding` | Rascunhos de onboarding self-service (pré-tenant) | `onboarding_drafts` |
| `public` | Só `__EFMigrationsHistory` e funções built-in (`gen_random_uuid`) | — |

O schema `platform` corresponde a `AdministrationConfiguration` (o nome "platform" descreve
melhor o escopo: dados da plataforma, não de um tenant). Um contexto pode ter uma única tabela
(`customers`, `notifications`, `onboarding`) — a divisão segue a **propriedade arquitetural**,
não a contagem de tabelas.

`Reports` não tem tabela (lê os outros contextos). `Payments` está dentro de `billing`.
`Features` está dentro de `plans`.

### 2. Configuração centralizada, sem `HasDefaultSchema`

`Nexora.Infrastructure.Persistence.DatabaseSchemas` expõe uma constante por schema. Cada
`IEntityTypeConfiguration` passa a chamar `ToTable("<tabela>", DatabaseSchemas.<Contexto>)`.

`HasDefaultSchema` global **não** é usado — ele jogaria tudo num schema só e anularia a divisão.
`__EFMigrationsHistory` continua em `public` (default do EF; mover a history table é uma decisão
separada e arriscada — fora deste escopo).

### 3. FK cross-schema é permitida e não é eliminada

O modelo é hub-and-spoke em torno de `tenancy.tenants` e `identity.users` — ~18 arestas de FK
cruzam contexto (ex.: `scheduling.appointments → catalog.professionals`,
`billing.subscriptions → plans.plans`, tudo tenant-scoped → `tenancy.tenants`). O PostgreSQL
suporta FK entre schemas nativamente. **Não se duplica tabela nem se cria segunda fonte de
verdade** para evitar o cruzamento. As FKs compostas `(TenantId, Id)` que impedem referência
cross-tenant (`professional_services`, `working_hours`, `blocked_periods`, `appointments`)
permanecem exatamente como estão — só passam a ser cross-schema.

### 4. Migração não-destrutiva

Uma migration (`…_OrganizeSchemasByContext`) faz `EnsureSchema` para os 10 schemas e
`RenameTable(name, schema: null → newSchema)` para cada tabela — que o Npgsql traduz para
`ALTER TABLE public.<t> SET SCHEMA <s>`. Essa operação é atômica e preserva dados, PK, FKs (de e
para a tabela), índices, unique constraints, índices parciais e OIDs. Não há `DROP`/`CREATE`, não
há sequences a mover (todas as PKs são `uuid` geradas no cliente).

O `Down()` reverte: `RenameTable(newSchema → public)` para cada tabela e `DropSchema` para os 10.

- **Banco do zero:** as migrations históricas criam as tabelas em `public`, a nova migration as
  move. As migrations históricas **não são reescritas** — representam estados anteriores a esta.
- **Banco existente (baseline):** a migration incremental move as tabelas já populadas.

### 5. Raw SQL

- `OnboardingService` usa `FromSqlInterpolated("SELECT * FROM onboarding_drafts … FOR UPDATE")` —
  passa a qualificar `onboarding.onboarding_drafts`.
- `PostgresAdvisoryLock` (`SELECT pg_advisory_xact_lock(k1,k2)`) não referencia tabela —
  inalterado. O lock de limite de plano por `(tenant, feature)` (ADR-0019 §10) mantém a semântica.
- Os `migrationBuilder.Sql(...)` das migrations históricas **não são tocados** — todos rodam antes
  desta migration, com as tabelas ainda em `public` e `search_path = public`.

## Alternativas

- **`HasDefaultSchema("nexora")` (schema único):** migration trivial e sem FK cross-schema, mas
  não entrega a divisão por contexto pedida.
- **Schema por tenant:** rejeitado em ADR-0002 (complexidade de migrations/operação); e não é o
  objetivo — aqui todos os tenants continuam compartilhando `scheduling.appointments` etc.
- **Reescrever/squashar migrations com schema embutido:** perde o caminho de upgrade; proibido.
- **Manter tabelas muito referenciadas por raw SQL em `public`:** só `onboarding_drafts` tinha
  raw SQL de tabela, e uma linha resolve — não justifica exceção.

## Consequências

- `\dn` + `\dt <schema>.*` no `psql` passam a refletir os módulos. `information_schema` e
  ferramentas de introspecção ganham a dimensão de contexto.
- Abre caminho (não usado agora) para `GRANT` por schema, filtros de replicação lógica e extração
  física de um módulo.
- Toda migration futura que use `migrationBuilder.Sql` referenciando tabela movida precisa
  **qualificar o schema** (ou setar `search_path`). Idem para novo raw SQL em serviços.
- O snapshot do EF muda em massa (anotação de schema por entidade) — mecânico, sem risco.
- Não altera ADR-0002: `TenantId` continua obrigatório, isolamento continua manual + FKs
  compostas, testes de isolamento e IDOR continuam válidos.
