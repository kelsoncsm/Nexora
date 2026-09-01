# ADR-0010 — Boundary entre catálogo de planos e assinatura

- Status: Accepted
- Data: 2026-09-01

## Contexto

A F5 precisa resolver features efetivas por tenant, mas a associação persistente Tenant→Plan pertence ao contexto de assinaturas da F9.

## Decisão

A F5 implementa `Plan`, `Feature`, `PlanFeature`, `TenantFeatureOverride` e `IFeatureAccessService`. O resolver obtém o plano vigente exclusivamente pelo contrato de leitura `ITenantPlanProvider`. Não serão criados `TenantPlanAssignment`, `PlanId` em `Tenant` ou `Subscription` antecipada. Testes da F5 usam fake/stub do provider.

Na F9, `Subscription` será a fonte persistente da associação Tenant→Plan e fornecerá a implementação persistente de `ITenantPlanProvider`.

## Consequências

- Não existe tabela provisória nem segunda fonte de verdade.
- O catálogo e os overrides podem ser desenvolvidos e testados independentemente do Billing.
- Até a F9, runtime sem provider persistente considera que o tenant não possui plano, aplicando somente override explícito quando existente.
