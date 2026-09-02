# ADR-0022 — Schema único `nexora` na database compartilhada `saas_dev`

## Status

Aceito — 2026-09-02. **Substitui o [ADR-0020](ADR-0020-postgresql-schema-organization.md).**
Refatoração de persistência + infraestrutura, sem mudança de regra de negócio.

## Contexto

O Nexora deixou de usar uma database PostgreSQL dedicada (`nexora`, tudo em `public`) e passou a
compartilhar a instância PostgreSQL 18 com outros sistemas. A database compartilhada é **`saas_dev`**
e contém também o schema **`dentalflow`** (de outro produto). `saas_dev` não pode ser recriada nem
apagada, e o schema `dentalflow` é intocável.

O ADR-0020 tinha acabado de mover as 31 tabelas de domínio para 10 schemas por bounded context
(`identity`, `tenancy`, `billing`, …). Numa database que agora convive com outro sistema, essa
divisão atrapalha mais do que ajuda: polui o `\dn`, multiplica as regras de `search_path`/`GRANT` e
não entrega isolamento real (isolamento continua por `TenantId`, ADR-0002). O DentalFlow, no mesmo
`saas_dev`, usa um único schema — o Nexora passa a seguir o mesmo padrão.

Quando esta decisão foi tomada, a base `saas_dev.nexora` **já havia sido migrada fisicamente**
(dump de `public` → schema `nexora`), com todos os dados, PKs, FKs, índices e o histórico de
migrations preservados, parado em `20260901210000_AddTenantManagePermission`.

## Decisão

### 1. Um único schema por ambiente

| Ambiente | Database | Schema |
|---|---|---|
| Aplicação (dev / staging / produção) | `saas_dev` | `nexora` |
| Testes de integração PostgreSQL | `saas_dev` | `nexoratest` |
| Outro sistema — **não tocar** | `saas_dev` | `dentalflow` |

`Nexora.Infrastructure.Persistence.DatabaseSchemas` expõe só `Application = "nexora"`,
`IntegrationTests = "nexoratest"` e o nome da tabela de histórico. Não há mais constante por
contexto. Bounded contexts permanecem como namespaces/pastas — divisão **lógica**.

### 2. `HasDefaultSchema` parametrizável, não `Search Path` sozinho

`NexoraDbContext.OnModelCreating` chama `modelBuilder.HasDefaultSchema(Schema)`, onde `Schema` vem de
`NexoraPersistenceOptions` (config `Database:Schema`, default `"nexora"`). Só o host de testes de
integração sobrescreve para `"nexoratest"`. Um `IModelCacheKeyFactory` inclui o schema na chave do
modelo. As queries EF saem qualificadas (`nexora."Users"`) — não dependem apenas do `search_path`.

A connection string ainda carrega `Search Path=<schema>`: as migrations históricas (anteriores a
esta mudança) são **schema-relativas** (nomes sem qualificação, resolvidos pelo `search_path`), e o
mesmo vale para o raw SQL (`OnboardingService`, seeds). Assim a mesma cadeia de migrations
inicializa tanto `nexora` quanto `nexoratest`.

### 3. Histórico de migrations no schema da aplicação

`MigrationsHistoryTable("__EFMigrationsHistory", <schema>)` em todos os pontos que abrem o provider
(DI de runtime, factory de design-time, factory de testes). O histórico do Nexora fica em
`saas_dev.nexora.__EFMigrationsHistory` (e `saas_dev.nexoratest.__EFMigrationsHistory` nos testes),
nunca em `public`.

### 4. Baseline suppression na migration `AddAuditLogTenantAndDetails`

As duas migrations que só existiam no `develop` local e nunca foram aplicadas
(`20260902111923_OrganizeSchemasByContext` — implementação do ADR-0020 — e
`20260902120702_AddAuditLogTenantAndDetails`) foram removidas com `dotnet ef migrations remove`
(sem reescrita de histórico Git). A migration de auditoria foi **regenerada** como
`20260902132646_AddAuditLogTenantAndDetails`.

Ao regenerar, o modelo EF já declarava `HasDefaultSchema("nexora")` enquanto o snapshot-baseline
estava em `public`. O diff produziu, além das 2 colunas de `audit_logs`, um `EnsureSchema("nexora")`
e **31 `RenameTable` (`public` → `nexora`)**. Essas operações de relocação de schema foram
**removidas deliberadamente** do corpo da migration:

- toda base `saas_dev` já foi movida fisicamente para `nexora` antes desta migration —
  reexecutar o `SET SCHEMA` falharia (`table ... is already in schema "nexora"`);
- as migrations históricas continuam schema-relativas, então a mesma cadeia ainda inicializa uma
  base nova em `nexora` ou `nexoratest` — o schema é **pré-criado pelo host** antes do
  `MigrateAsync` (ver item 5);
- o `.Designer.cs` e o `NexoraDbContextModelSnapshot.cs` regenerados **mantêm** o estado final
  (default schema `nexora`, 31 tabelas nele) como baseline lógico para migrations futuras.

O `Up()` final contém apenas `AddColumn Details` (jsonb) e `AddColumn TenantId` (uuid) em
`audit_logs`, sem `schema:` explícito. O `Down()`, apenas os dois `DropColumn`. Há um comentário
`<remarks>` na própria migration explicando essa supressão — **a edição é intencional, não um
patch manual acidental.**

Como consequência, ao rodar `MigrateAsync` contra o schema de testes (`nexoratest`) o modelo de
runtime difere do snapshot só no default schema, o que o EF sinaliza como
`PendingModelChangesWarning`. Esse aviso é ignorado **apenas** na `PostgresApiFactory`
(`ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))`). A verificação
real de drift permanece no CI via `dotnet ef migrations has-pending-model-changes` (que roda contra
o schema `nexora` e passa).

### 5. Criação de schema com allowlist — nunca genérica

`NexoraSchemaGuard` só aceita `nexora` e `nexoratest`; `CreateIfMissingAsync` mapeia cada um para um
literal fixo `CREATE SCHEMA IF NOT EXISTS "..."` (nenhum identificador é interpolado em DDL) e
**nunca** faz `DROP`. O startup de desenvolvimento (`DatabaseInitialization`) pré-cria o schema da
aplicação antes do `MigrateAsync`. Uma configuração apontando para `dentalflow`, `public` ou
qualquer outro nome lança exceção.

### 6. Proteção dos testes

`TestSchemaGuard.EnsureIntegrationTestReset` é chamado antes de qualquer
`DROP SCHEMA nexoratest CASCADE` e aborta a menos que, simultaneamente:
`Database == "saas_dev"`, `Search Path == "nexoratest"` e schema configurado `== "nexoratest"`.
Em runtime normal, nenhuma rotina executa `DROP SCHEMA`. Não há `EnsureDeleted`, `EnsureCreated`
nem `Respawn` no código.

## Alternativas

- **Manter 10 schemas por contexto (ADR-0020):** rejeitado — numa database compartilhada com o
  DentalFlow, multiplica a superfície operacional sem entregar isolamento.
- **Database dedicada `nexora`:** rejeitado — o objetivo é justamente consolidar na instância
  compartilhada `saas_dev`.
- **Só `Search Path`, sem `HasDefaultSchema`:** rejeitado — queries deixariam de ser qualificadas;
  um `search_path` errado poderia ler/escrever no schema errado silenciosamente.
- **Reescrever a migration para fazer o `SET SCHEMA` de forma idempotente:** desnecessário — o
  movimento físico já aconteceu; uma migration que não faz nada em toda base real é ruído.
- **Squashar as migrations históricas com o schema embutido:** rejeitado — perde o caminho de
  inicialização schema-relativa que hoje serve `nexora` e `nexoratest` com a mesma cadeia.

## Consequências

- Todo raw SQL / `migrationBuilder.Sql` novo que referencie tabela deve ficar **sem qualificação**
  (resolve por `search_path`) ou usar o schema ativo — nunca hardcode `nexora`.
- Nova migration gerada depois desta terá as operações qualificadas em `nexora` (default de
  design-time). Se precisar rodar também em `nexoratest`, mantenha-a schema-relativa (remova o
  `schema:` das operações), como foi feito aqui.
- `dotnet ef` em design-time exige `ConnectionStrings__NexoraDatabase` apontando para `saas_dev`
  com `Search Path=nexora`; opcionalmente `Database__Schema`.
- F5 / `dotnet run`: connection string via user-secrets, com `Search Path=nexora`
  (`db-set-password.ps1` já grava assim). `docker compose`: `.env` com `POSTGRES_DB=saas_dev`.
- Roles PostgreSQL least-privilege (`nexora_app` só em `nexora`, `nexora_test` só em `nexoratest`,
  ambos sem acesso a `dentalflow`) ficam como **trabalho futuro separado** — hoje há um único role
  `nexora`, dono de tudo. Os guards de aplicação e de teste já cobrem o risco de reset destrutivo.
- Não altera ADR-0002 (multi-tenancy por `TenantId`) nem ADR-0019 (feature enforcement).
