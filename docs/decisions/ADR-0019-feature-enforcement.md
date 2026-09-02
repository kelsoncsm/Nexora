# ADR-0019 — Enforcement de planos e features (P1.1)

- Status: Accepted
- Data: 2026-09-02

## Contexto

A F5 entregou `Feature`, `Plan`, `PlanFeature`, `TenantFeatureOverride`, `IFeatureAccessService` e, na F9, `PersistentTenantPlanProvider` (Tenant→Plan pela `Subscription` vigente). A cadeia de resolução `override → PlanFeature → (false, null)` existe e é testada isoladamente. Porém **nenhum endpoint, filtro, serviço ou guard chamava `ResolveAsync`**: um tenant sem plano acessava todos os módulos, nenhum limite (`FeatureAccess.Limit`) era verificado, e desativar um plano/feature no Platform Admin não tinha efeito em runtime. O critério da F5 ("o acesso aos módulos é controlado por plano/configuração"), o `security.md` ("a decisão efetiva considera identidade, escopo, associação ao tenant, permissão **e feature aplicável**") e o `api-guidelines.md` não eram cumpridos.

## Decisão

### 1. Duas camadas de enforcement, ambas no backend

- **Acesso ao módulo (feature on/off):** um *endpoint filter* `RequireFeature("CODE")` aplicado por grupo de rotas, executado **depois** de `UseAuthentication`, `UseTenantContext` e `UseAuthorization` — quando o filtro roda, o `ITenantContext` já está inicializado e o usuário já é membro autorizado do tenant.
- **Limites de plano (contagem):** verificados dentro dos serviços de criação (`CustomerService.CreateAsync`, `CatalogService.CreateProfessionalAsync`), porque o limite depende de uma contagem tenant-scoped que vive nessa camada.

Não se usa `IAuthorizationHandler`/policy: **feature ≠ permissão** (`security.md`). Misturar as duas confunde 403-de-permissão com 403-de-plano e complica os testes negativos.

### 2. `IFeatureAccessService` continua a única fonte de verdade

O filtro chama `IFeatureAccessService.ResolveAsync(tenantId, code)` (cadeia override → PlanFeature → `(false, null)`) e cacheia o resultado em `HttpContext.Items` por requisição, para o mesmo request não resolver duas vezes.

### 3. Mapa feature → módulo é estático em código (contrato de deploy)

`Nexora.Application.Plans.FeatureCodes`:

| Código | Grupo de rotas gateado |
|---|---|
| `CUSTOMERS` | `/api/v1/customers` |
| `SCHEDULING` | `/api/v1/appointments`, `/api/v1/working-hours`, `/api/v1/blocked-periods`, `/api/v1/scheduling/context` |
| `PROFESSIONALS` | `/api/v1/professionals` |
| `SERVICES` | `/api/v1/services` |
| `REPORTS` | `/api/v1/reports/overview` (tenant) |

**Nunca gateado:** identity, tenancy/tenant-admin (`/api/v1/tenant/**`, `/api/v1/me/tenants`), onboarding, vertical-setup, billing/subscription (`/api/v1/subscription`, `/api/v1/billing/**`) e todo `/api/v1/admin/**` (`/api/v1/admin/reports/overview` permanece só sob `PlatformAdmin`).

Adicionar ou remover um código aqui é decisão arquitetural, não configuração. O mapa não vive em banco.

### 4. Todos os cinco grupos passam pelo filtro uniformemente

Se o tenant tem ou não a feature é decidido por plano/override. **Consequência explícita:** um tenant **sem `Subscription` vigente** (`GetCurrentPlanIdAsync → null`) resolve toda feature gateada para `(false, null)` → **403 em todos os cinco grupos**. Isso é o comportamento pretendido de "o plano controla os módulos" (critério da F5). O onboarding já cria uma `Subscription` em trial, então tenants onboarded estão cobertos; tenants criados por `POST /api/v1/tenants` sem plano ficam sem acesso operacional, por design.

### 5. Contrato HTTP

- Feature ausente/desabilitada → **403** `application/problem+json`, `code = "feature_not_in_plan"`, `feature = "<CÓDIGO>"`. 403 (não 402/404) porque o usuário está autenticado e autorizado por permissão — é o *plano* que nega, e não se revela/oculta a existência do recurso.
- Limite de plano atingido → **409** `application/problem+json`, `code = "plan_limit_reached"`, com `feature`, `limit` e `current`. 409 alinha com o resto da API (conflitos de agenda/unicidade usam 409; o projeto não adota 422).

Ambos são exceções (`FeatureNotInPlanException`, `PlanLimitExceededException`) mapeadas pelo `GlobalExceptionHandler`, como todo o resto.

### 6. Descoberta pelo frontend

`GET /api/v1/identity/me` passa a incluir, **quando há sessão de tenant**, um objeto `features` com cada código do mapa:

```json
{ "id": "...", "email": "...", "permissions": ["..."],
  "features": { "CUSTOMERS": { "enabled": true, "limit": null },
                "PROFESSIONALS": { "enabled": true, "limit": 3 },
                "REPORTS": { "enabled": false, "limit": null } } }
```

Fora de uma sessão de tenant o shape não muda. O frontend usa isso só para esconder menu/rotas — o filtro do backend é a autoridade.

### 7. Esconder menus/rotas (apenas UX)

- `featureGuard('CODE')` (factory de `CanActivateFn`) redireciona para `/403` quando a feature está desabilitada. Aplicado a `/clientes`, `/profissionais`, `/servicos`, `/agenda`, `/relatorios`.
- `app.ts navGroups` mostra cada item só quando `permissão ∧ hasFeature(código)`.
- Navegar direto na API sem a feature → 403 sempre. O guard só evita a tela quebrada.

### 8. Seed

Uma migration data-only (`20260902000000_SeedFeatureCatalog`, mesmo padrão de `AddTenantManagePermission` — SQL, `ON CONFLICT ("Code") DO NOTHING`) insere as cinco `Feature` rows (`CUSTOMERS`, `SCHEDULING`, `PROFESSIONALS`, `SERVICES`, `REPORTS`), `IsActive = true`. **Sem planos, sem `PlanFeature`, sem preços, sem limites** — o catálogo comercial continua criado pelo Platform Admin em runtime (MASTER_PLAN §43).

### 9. Impactos

- **Subscription:** expirar/cancelar uma assinatura passa a **cortar** o acesso aos módulos na próxima requisição (a graça de 7 dias já é avaliada pelo `PersistentTenantPlanProvider`).
- **TenantFeatureOverride:** vira a alavanca de suporte/exceção comercial (`ConfigureOverrideAsync` já existe). Ganha peso operacional → deveria gerar `AuditLog` — **follow-up P2.9, fora deste escopo**.
- **Platform Admin:** desativar uma `Feature` ou um `PlanFeature` passa a ter efeito imediato no runtime dos tenants daquele plano. Os endpoints `/api/v1/admin/**` continuam fora de qualquer feature gate.

## Alternativas

- **`IAuthorizationHandler`/policy por feature:** feature não é permissão; separá-las mantém os testes negativos e as mensagens claras.
- **Mapa feature→módulo em banco:** é contrato de deploy, não configuração comercial; manter em código evita uma segunda fonte de verdade.
- **Gate nos controllers/handlers:** polui os handlers; o endpoint filter no grupo cobre todas as rotas com uma linha.
- **402 Payment Required / 404 para feature-off:** 403 é o correto — autenticado e autorizado por permissão, o plano é que nega; 404 esconde/revela existência sem necessidade.
- **Sempre-on para módulos "core":** rejeitado — o MVP comercial (MASTER_PLAN §31) já diferencia planos por módulo (ex.: Relatórios só no Profissional). Todos os cinco passam pelo filtro; o plano decide.

## Consequências

- O critério da F5 passa a ser atendido em runtime.
- Qualquer tenant sem `Subscription` perde o acesso operacional até ter um plano — esperado, mas exige que provisionamento/seed de planos exista antes de operar (Platform Admin).
- Testes que criam tenant "cru" e batem em módulo agora montam um `Plan` + `Subscription` + `PlanFeature` via `TestFeatureCatalog`.
- `AuditLog` em override/plan/feature fica como dívida registrada (P2.9).
