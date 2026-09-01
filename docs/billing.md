# Billing e pagamentos operacionais

## Separação de contextos

O Nexora mantém dois domínios distintos:

| Billing da plataforma | Financeiro/pagamento operacional |
|---|---|
| relação Nexora ↔ tenant | relação tenant ↔ cliente |
| plano e assinatura SaaS | agendamento, pedido ou venda |
| invoice/charge e `BillingPayment` | `Payment` operacional |
| inadimplência afeta assinatura/features | pagamento afeta operação do tenant |
| administração em escopo de plataforma | dados isolados por tenant |

Entidades, serviços, contratos, endpoints e relatórios não devem misturar esses contextos. Nomes ambíguos como `Payment` no Billing devem ser qualificados no código e nos contratos.

## Billing da plataforma

O contexto cobre `Tenant`, `Plan`, `Subscription`, eventos de assinatura, cobrança/invoice e pagamento da cobrança. A F9 consolidou `Subscription` como fonte persistente única da relação Tenant → Plan, com estados `Trialing`, `Active`, `PastDue`, `Canceled` e `Expired`.

Para o MVP, o trial persistido dura 14 dias, a periodicidade é mensal ou anual, o cancelamento normal ocorre ao fim do período e a inadimplência possui tolerância de sete dias. `Trialing`, `Active` e `PastDue` dentro da tolerância concedem entitlement; cancelamento agendado preserva o plano até `CurrentPeriodEnd`. Transições administrativas exigem PlatformAdmin, geram eventos e auditoria e não simulam pagamento externo. A decisão completa está em `ADR-0013`.

Planos, features, overrides e limites são dados/configuração. Nomes de planos e valores apresentados no plano mestre são exemplos ou placeholders, não regras a hardcodar.

## Pagamentos operacionais

O contexto Payments cobre valores pagos por clientes do tenant em relação a agendamento/pedido/venda. Seus dados são tenant-scoped e obedecem integralmente ao isolamento. Ele não decide o estado da assinatura SaaS.

## Gateways e webhooks

Integrações usam a porta `IPaymentGateway`; regras de negócio não dependem diretamente de SDKs. O adaptador inicial é o Mercado Pago. Webhooks validam autenticidade e freshness da assinatura, são idempotentes, toleram duplicidade/reprocessamento e não usam redirect do navegador como confirmação definitiva.

A F10 escolheu Mercado Pago como primeiro adaptador, Brasil/BRL como mercado inicial e `PlanPrice` como fonte interna de preço. Cada `BillingInvoice` é a obrigação comercial única identificada por assinatura e período de cobertura, preservando snapshots de plano, intervalo, valor, moeda, `CoverageStart` e `CoverageEnd`; uma invoice pode ter várias tentativas em `BillingPayment`. Pagamentos reconciliados nunca obtêm autoridade diretamente do payload do webhook. Consulte `ADR-0014`.

Identificadores externos de evento e operação serão persistidos de modo a sustentar idempotência e reconciliação. Payloads e logs não devem expor secrets nem dados de cartão.

## Auditoria e consistência

Mudanças relevantes de assinatura e pagamento geram histórico/auditoria. Checkout usa identidade persistente da obrigação, constraint única, transação PostgreSQL e chave de idempotência estável. Métricas como MRR e churn só serão produzidas quando houver fonte confiável e sem misturar faturamento operacional dos tenants.

## Decisões futuras fora do MVP atual

- países, moedas, impostos e emissão fiscal;
- expansão de moedas e política comercial de reembolso;
- provedores adicionais.
