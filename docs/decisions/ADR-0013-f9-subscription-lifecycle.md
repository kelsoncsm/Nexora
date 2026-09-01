# ADR-0013 — Lifecycle de Subscription e plano vigente

- Status: Accepted
- Data: 2026-09-01

## Contexto

A F5 definiu o catálogo de planos e a abstração `ITenantPlanProvider`, mas adiou corretamente a associação persistente Tenant → Plan. A F9 precisa introduzir essa fonte única sem antecipar gateway, cobrança ou pagamentos da F10.

## Decisão

`Subscription` é a única associação persistente entre `Tenant` e `Plan`. Não existe `TenantPlanAssignment` nem `PlanId` em `Tenant`. A criação administrativa da primeira assinatura inicia um trial de 14 dias no plano e periodicidade explicitamente escolhidos.

A máquina de estados permite `Trialing`, `Active`, `PastDue`, `Canceled` e `Expired`. Ativação/regularização, troca imediata de plano, inadimplência e cancelamento imediato são operações exclusivas de Platform Admin e geram `SubscriptionEvent` e `AuditLog`. Cancelamento normal ocorre ao final do período.

O provider persistente concede entitlement para trial válido, assinatura ativa e `PastDue` dentro do grace period de sete dias. Trial vencido, `PastDue` fora do grace, período ativo encerrado sem renovação, cancelamento concluído e expiração não resolvem plano. A avaliação e as transições ficam no domínio/serviço, não em controllers.

Um índice parcial único no PostgreSQL impede mais de uma assinatura vigente (`Trialing`, `Active` ou `PastDue`) por tenant, preservando assinaturas encerradas como histórico.

## Alternativas

- `Tenant.PlanId`: rejeitado por criar uma segunda fonte de verdade.
- Associação provisória: rejeitada pela boundary aprovada na F5.
- Pagamento fictício para ativação: rejeitado; ativação administrativa não afirma pagamento externo.
- Gateway e invoices na F9: adiados para F10.

## Consequências

- Features mudam imediatamente quando o plano da assinatura muda.
- Eventos futuros de gateway deverão chamar comandos da mesma máquina de estados.
- Não há proration nem troca programada na F9.
- Billing SaaS permanece separado dos pagamentos operacionais do tenant.
