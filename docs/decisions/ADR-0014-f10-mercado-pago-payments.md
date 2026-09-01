# ADR-0014 — Pagamentos SaaS com Mercado Pago

- Status: Accepted
- Data: 2026-09-01

## Contexto

A F9 estabeleceu o lifecycle de Subscription, mas não criou preço comercial, invoices nem integração financeira. A F10 precisa cobrar o tenant sem tornar o gateway autoridade do domínio ou misturar Billing SaaS com pagamentos operacionais.

## Decisão

Mercado Pago é o primeiro adaptador, isolado em Infrastructure atrás de `IPaymentGateway`. O MVP opera no Brasil em BRL, persistido explicitamente. `PlanPrice` versiona preço por plano, periodicidade e moeda; `BillingInvoice` preserva o snapshot e `BillingPayment` registra o resultado reconciliado.

O checkout resolve preço exclusivamente no backend. Chamadas de criação usam chave idempotente. Webhooks são validados por HMAC a partir de `x-signature`, `x-request-id` e `data.id`, deduplicados em `ProcessedWebhookEvent` e reconciliados consultando o gateway. Somente o serviço de Billing traduz o resultado para comandos da máquina de estados da Subscription.

Credenciais entram apenas por configuração externa. Testes automatizados usam fake de `IPaymentGateway` e nunca realizam cobrança real.

## Alternativas

- Tipos/SDK do Mercado Pago no domínio: rejeitado por acoplamento.
- Preço enviado pelo frontend ou retornado pelo gateway: rejeitado por falta de autoridade e integridade histórica.
- Stripe/Asaas simultaneamente: adiado; novos adaptadores poderão implementar o mesmo contrato.
- SDK oficial: não adotado neste recorte; HTTP direto cobre as poucas operações necessárias sem fazer tipos externos vazarem.

## Consequências

- Alterar um preço não modifica invoices históricas.
- PIX, boleto e cartão usam detalhes próprios; somente cartão tokenizado pode participar de fluxos recorrentes compatíveis.
- Eventos antigos não fazem o estado financeiro regredir.
- Refund parcial e credenciais/testes externos permanecem fora do caminho automatizado do MVP.
