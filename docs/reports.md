# Relatórios

## Escopo da F12

O Nexora expõe indicadores derivados apenas de dados já persistidos. Não existem métricas estimadas ou fontes externas.

### Tenant

`GET /api/v1/reports/overview?from=<instant>&to=<instant>` exige tenant autenticado e `reports.read`. Retorna clientes, agendamentos por status e produtividade por profissional. Não expõe receita enquanto não existir uma fonte de pagamentos operacionais do tenant; `BillingInvoice` pertence ao Billing SaaS e não representa receita do negócio. Todas as consultas aplicam `TenantId` do contexto autenticado.

### Plataforma

`GET /api/v1/admin/reports/overview?from=<instant>&to=<instant>` exige Platform Admin sem tenant selecionado. Retorna tenants, assinaturas por estados relevantes e receita SaaS paga.

O intervalo é UTC, usa limite superior exclusivo e não pode exceder 366 dias. A moeda atual é BRL, coerente com a decisão do Billing MVP. O frontend oferece filtros de data nas rotas `/relatorios` e `/admin/relatorios`.

## Segurança e desempenho

- O frontend não fornece `TenantId`.
- Relatórios tenant-scoped não consultam dados de outro tenant.
- O período obrigatório limita o custo das consultas.
- Índices existentes de agendamento e Billing sustentam os filtros atuais; novos índices exigem evidência de consulta.
