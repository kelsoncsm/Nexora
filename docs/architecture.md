# Arquitetura do Nexora

## Objetivo e escopo

O Nexora é um SaaS multi-nicho, multi-tenant e configurável. Esta visão define os limites arquiteturais iniciais; detalhes físicos da solution serão fechados na F1 sem alterar estas decisões.

## Estilo arquitetural

O sistema será um **Modular Monolith**: um deploy lógico inicial, dividido internamente em módulos coesos e com limites explícitos. Não haverá microservices no MVP. Uma eventual extração exigirá necessidade demonstrável e ADR próprio.

Dentro dos módulos, aplicam-se Clean Architecture, DDD pragmático, SOLID e dependência orientada ao domínio. Controllers coordenam HTTP; regras ficam no domínio/aplicação; infraestrutura implementa portas definidas internamente.

## Módulos e responsabilidades

| Módulo | Responsabilidade principal |
|---|---|
| Identity | usuário, credenciais, sessão, roles e permissions |
| Tenancy | tenant, associação usuário-tenant e contexto do tenant |
| Administration | operações exclusivas da plataforma e auditoria administrativa |
| Plans | catálogo de planos e limites comerciais |
| Features | catálogo, habilitação e overrides de funcionalidades |
| Billing | assinatura, cobrança e pagamento do SaaS |
| Customers | clientes pertencentes a tenants |
| Professionals | profissionais pertencentes a tenants |
| Services | serviços oferecidos pelos tenants |
| Scheduling | disponibilidade, bloqueios e agendamentos |
| Payments | pagamentos operacionais de clientes aos tenants |
| Notifications | entrega de notificações por canais suportados |
| Reports | projeções e relatórios a partir de dados autorizados |

Os módulos não acessam detalhes internos uns dos outros. Integração ocorre por contratos explícitos, mantendo dependências acíclicas. Não se introduzirá mensageria distribuída por antecipação. A propriedade exata de entidades compartilhadas e o empacotamento físico serão refinados antes de cada fase afetada, por ADR se mudarem os limites acima.

## Fluxo de dependências

```text
Angular -> REST /api/v1 -> API/Application -> Domain
                              |                ^
                              v                |
                         Infrastructure -------+
                              |
                         PostgreSQL
```

O frontend oferece UX, mas não é autoridade para autenticação, autorização, tenancy, preços, status ou limites.

## Persistência

PostgreSQL será o banco principal e Entity Framework Core o ORM. O modelo inicial usa banco e tabelas compartilhados; dados tenant-scoped carregam `TenantId`. Mudanças de schema serão migrations versionadas. Índices serão definidos por consultas reais, com atenção a chaves iniciadas por `TenantId`.

## Configurabilidade

Segmentos descrevem o contexto comercial, mas não devem criar condicionais espalhadas nem forks. Comportamento disponível resulta de configuração, plano, feature, override e capabilities encapsuladas. Preços, limites e políticas comerciais não serão hardcoded.

## Contratos e integração

- APIs REST versionadas em `/api/v1` e DTOs independentes das entidades de persistência.
- Erros consistentes e sem stack trace em produção.
- Gateways externos ficam atrás de portas, como `IPaymentGateway`.
- Webhooks exigem autenticidade, idempotência e tolerância a duplicidade/reprocessamento.
- Datas persistidas representam instantes de forma não ambígua; exibição e regras locais usam o timezone configurado do tenant. O mapeamento técnico exato será definido antes da F8.

## Observabilidade e operação

Logging estruturado deve carregar, quando disponível, `CorrelationId`, `RequestId`, `UserId`, `TenantId` e operação, sem segredos. Health checks, tratamento global de exceções, métricas e auditoria serão introduzidos nas fases previstas pelo roadmap.

## Restrições arquiteturais

- nenhum microservice ou broker no MVP sem ADR aprovado;
- nenhum acesso cross-tenant implícito;
- Platform Admin e Tenant Admin são escopos distintos;
- Billing SaaS e pagamentos operacionais são contextos distintos;
- contratos públicos e limites de módulo só mudam mediante decisão controlada.

## Decisões em aberto para fases futuras

- versão LTS exata do .NET e versões compatíveis da stack, na F1;
- organização física dos projetos/módulos, na F1;
- biblioteca de UI, na F1;
- provedor inicial de pagamento, antes da F10;
- estratégia técnica detalhada de timezone, antes da F8;
- infraestrutura de produção, CI/CD, backup e restore, até a F14.
