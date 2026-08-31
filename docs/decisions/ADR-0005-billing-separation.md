# ADR-0005 — Separação entre Billing SaaS e pagamentos operacionais

## Contexto

O Nexora cobra tenants pela assinatura e também gerencia pagamentos que clientes fazem aos negócios. Os fluxos têm atores, regras, riscos e relatórios diferentes.

## Decisão

Manter Billing da plataforma e Payments operacional como módulos/contextos distintos, com modelos, contratos, serviços e endpoints separados. `BillingPayment` representa pagamento de cobrança SaaS; `Payment` representa pagamento operacional tenant-scoped.

## Alternativas

- Modelo único de pagamento: reduz entidades inicialmente, mas mistura ownership, autorização, reconciliação e métricas.
- Contextos separados escolhidos: algum código conceitualmente semelhante pode existir, porém invariantes permanecem claras.

## Consequências

MRR/inadimplência não se confundem com faturamento do tenant; autorizações e tenancy são independentes; integrações podem compartilhar abstrações técnicas sem compartilhar regras de domínio. Mudanças comerciais de um contexto não contaminam o outro.

## Status

Accepted
