# ADR-0021 — Política de audit log de ações sensíveis

## Status

Aceito — 2026-09-02. Fecha o P2.9 da auditoria. Sem mudança de regra funcional, RBAC, feature
enforcement, multi-tenancy ou organização de schemas (ADR-0020).

## Contexto

`AuditLog` (`platform.audit_logs`) existia mas só gravava `tenant.activate/deactivate`,
`segment.create/update`, `plan_price.changed` e as ações administrativas de `Subscription` (via
`SubscriptionService.Record`). Ficavam **sem registro**: mudança de papel de membro, desativação de
membro, alteração de permissões de role, `TenantFeatureOverride`, `PlanFeature` e o `checkout`
tenant-facing. Com o enforcement de plano em runtime (ADR-0019) e billing/members agora sob
`tenant.manage` (P2.1/P2.2), essas ações passaram a ter efeito real e precisam de trilha.

## Decisão

### 1. Ações auditadas

| Ação | Código (`AuditActions`) | Escopo | Ator | `Details` (JSON) |
|---|---|---|---|---|
| Trocar papel de membro | `member.role_changed` | tenant | usuário autenticado (tenant admin) | `membershipId`, `oldRoleId`, `newRoleId` |
| Desativar membro | `member.deactivated` | tenant | idem | `membershipId`, `wasActive` |
| Alterar permissões de role | `role.permissions_changed` | tenant | idem | `roleId`, `added[]`, `removed[]` |
| Configurar `TenantFeatureOverride` | `tenant_feature_override.configured` | tenant (o tenant alvo) | platform admin | `featureCode`, `oldEnabled`, `newEnabled`, `oldLimit`, `newLimit` |
| Configurar `PlanFeature` | `plan_feature.configured` | **global** (`TenantId = null`) | platform admin | `planId`, `featureCode`, `oldEnabled`, `newEnabled`, `oldLimit`, `newLimit` |
| Solicitar checkout | `billing.checkout_requested` | tenant | usuário autenticado | `subscriptionId`, `invoiceId`, `planId`, `amount`, `currency`, `paymentMethod` |
| Ações administrativas de subscription | `subscription.*` (já existiam) | tenant (agora com `TenantId`) | platform admin | sem `Details` — o detalhe fica em `SubscriptionEvent` (coluna `text`) |

**Não auditadas** (decisão explícita):
- Transições de subscription feitas por webhook/worker de sistema — já cobertas por
  `SubscriptionEvent` (`SubscriptionService.Record` só grava `AuditLog` quando há ator humano). Não
  se duplica a mesma transição em duas tabelas.
- `GET` de qualquer natureza.
- Criação/renome de role sem mudança de permissão, atualização de perfil da empresa — fora do P2.9.
- Alteração de permissões que resulta em **diff vazio** (`added` e `removed` ambos vazios) — não gera
  evento.

### 2. Ator humano vs sistema

Todo evento do P2.9 tem **ator humano** (`AuditLog.ActorUserId` continua `NOT NULL`). O ator vem do
contexto autenticado (`ITenantContext.UserId` para tenant, `ClaimsPrincipal` para platform admin),
**nunca do body**. Não foi preciso relaxar `ActorUserId` para nullable — o que exigiria auditoria de
sistema é subscription lifecycle, que fica em `SubscriptionEvent`.

### 3. `TenantId` no evento

Coluna nova `TenantId uuid NULL`. Preenchida com o tenant alvo em ações tenant-scoped e em ações de
plataforma sobre um tenant específico (override). `NULL` para ações de catálogo global (`PlanFeature`)
e para os eventos pré-P2.9. **Sem FK** — o audit log é append-only e deve sobreviver a qualquer
mudança nas entidades referenciadas.

### 4. Atomicidade

`IAuditLogWriter.Record(...)` **adiciona a linha ao mesmo `NexoraDbContext`** que a mutação de
negócio. A gravação acontece no **mesmo `SaveChangesAsync`/transação** do caso de uso. Se a mutação
falhar e reverter, o `AuditLog` não é persistido. Não há `try/catch` engolindo falha do audit para as
ações de member role/deactivate, role permissions, feature override e plan feature (§16 do prompt).
Nenhuma mensageria/outbox nova — reusa o DbContext único.

### 5. Eventos estruturados, sem segredo

`Details` é uma coluna `jsonb NULL` com um objeto estável (`camelCase`, ids e códigos). **Nunca**
contém senha, hash, token, chave de webhook, dado de cartão, payload bruto do gateway, URL secreta de
checkout, e-mail/nome quando o id/código basta. `billing.checkout_requested` registra o
`invoiceId`/`subscriptionId` internos e o valor/moeda — **não** o `checkoutUrl`, o token nem a
resposta do gateway.

### 6. Idempotência

`billing.checkout_requested` só é gravado quando um **novo** checkout de gateway é efetivamente
criado. Os caminhos de curto-circuito de `CreateCheckoutAsync` (pagamento pendente/pago já existe,
retorno idempotente) **não** geram evento. Webhook continua protegido por `ProcessedWebhookEvent`; a
transição de estado, quando `estado anterior == novo`, não vira evento.

### 7. Abstração

`IAuditLogWriter` (Application) + `AuditLogWriter` (Infrastructure), um método `Record`. Usada pelos
pontos novos (`TenancyService`, `PlanCatalogService`, `BillingPaymentService`). Os pontos que já
gravavam direto (`AdministrationService`, `SetPriceAsync`) **não foram migrados** — funcionam e são
testados; unificar é um follow-up opcional, fora deste escopo. `SubscriptionService.Record` ganhou
`TenantId` no `AuditLog` que já emitia.

### 8. Isolamento e leitura

Nenhum endpoint tenant de audit é criado nesta rodada. A única leitura continua sendo
`GET /api/v1/admin/audit-logs` (policy `PlatformAdmin`), agora expondo `TenantId` e `Details`.
`AuditLog` mora em `platform` (ADR-0020) — inalterado.

### 9. Retenção

Fora de escopo. Hoje o reader corta em 200 registros mais recentes; política de retenção/arquivamento
fica para uma rodada futura.

## Alternativas

- **Outbox/event sourcing para audit** — rejeitado (§23 do prompt); o DbContext único já dá
  atomicidade.
- **`ActorUserId` nullable + ator "system"** — desnecessário; nenhum evento do P2.9 é de sistema.
- **Migrar todos os call sites existentes para `IAuditLogWriter`** — churn em código testado e
  funcionando, sem ganho no escopo do P2.9.
- **FK `audit_logs.TenantId → tenancy.tenants`** — rejeitado; audit log não deve depender do ciclo de
  vida da entidade referenciada.

## Consequências

- Migração aditiva `AddAuditLogTenantAndDetails` (`TenantId`, `Details` em `platform.audit_logs`),
  não-destrutiva, sem mover/renomear tabela.
- `AssignRoleAsync` / `SetRolePermissionsAsync` / `ConfigureOverrideAsync` /
  `ConfigurePlanFeatureAsync` / `CreateCheckoutAsync` ganharam parâmetro de ator (e correlação onde
  aplicável) — plumbing, mesmo padrão de `DeactivateMembershipAsync` e `AdministrationService`.
- Linhas de `AuditLog` anteriores ao P2.9 têm `TenantId = null` — esperado.
- `AuditView` / `GET /admin/audit-logs` agora inclui `tenantId` e `details`.
