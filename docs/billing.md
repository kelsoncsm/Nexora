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

O contexto cobre `Tenant`, `Plan`, `Subscription`, eventos de assinatura, cobrança/invoice e pagamento da cobrança. Estados conceituais previstos incluem `Trialing`, `Active`, `PastDue`, `Cancelled` e `Expired`, mas regras de transição, trial, renovação, cancelamento, impostos, moeda, preços e tolerância à inadimplência ainda exigem requisitos comerciais antes da F9.

Planos, features, overrides e limites são dados/configuração. Nomes de planos e valores apresentados no plano mestre são exemplos ou placeholders, não regras a hardcodar.

## Pagamentos operacionais

O contexto Payments cobre valores pagos por clientes do tenant em relação a agendamento/pedido/venda. Seus dados são tenant-scoped e obedecem integralmente ao isolamento. Ele não decide o estado da assinatura SaaS.

## Gateways e webhooks

Integrações futuras usam uma porta como `IPaymentGateway`; regras de negócio não dependem diretamente de SDKs. Apenas um provedor será escolhido para o MVP antes da F10. Webhooks deverão validar autenticidade, ser idempotentes, tolerar duplicidade e reprocessamento, considerar eventos fora de ordem e não usar redirect do navegador como confirmação definitiva.

Identificadores externos de evento e operação serão persistidos de modo a sustentar idempotência e reconciliação. Payloads e logs não devem expor secrets nem dados de cartão.

## Auditoria e consistência

Mudanças relevantes de assinatura e pagamento geram histórico/auditoria. Processamento deverá definir fronteiras transacionais e comportamento diante de falhas antes da implementação. Métricas como MRR e churn só serão produzidas quando houver fonte confiável e sem misturar faturamento operacional dos tenants.

## Decisões pendentes

- provedor de pagamento do MVP;
- países, moedas, impostos e emissão fiscal;
- preços, limites e duração de trial;
- política de cobrança, retry, grace period, cancelamento e reembolso;
- origem de verdade e reconciliação do estado financeiro.

Essas decisões não bloqueiam a F0 e devem ser aprovadas antes das fases F9/F10 afetadas.
