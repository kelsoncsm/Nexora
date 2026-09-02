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

PostgreSQL será o banco principal e Entity Framework Core o ORM. O modelo usa banco e tabelas compartilhados; dados tenant-scoped carregam `TenantId`. A aplicação vive na database compartilhada `saas_dev` (ao lado do schema `dentalflow`, de outro sistema) e ocupa **um único schema `nexora`**, via `HasDefaultSchema("nexora")` + `Search Path` (ADR-0022, substitui a divisão por schema de contexto do ADR-0020). Os testes de integração ficam confinados ao schema `nexoratest` e nunca tocam `nexora`/`dentalflow`. Bounded contexts continuam sendo uma divisão lógica do código, não física. Mudanças de schema serão migrations versionadas. Índices serão definidos por consultas reais, com atenção a chaves iniciadas por `TenantId`.

## Configurabilidade

Segmentos descrevem o contexto comercial, mas não devem criar condicionais espalhadas nem forks. Comportamento disponível resulta de configuração, plano, feature, override e capabilities encapsuladas. Preços, limites e políticas comerciais não serão hardcoded.

## Contratos e integração

- APIs REST versionadas em `/api/v1` e DTOs independentes das entidades de persistência.
- Erros consistentes e sem stack trace em produção.
- Gateways externos ficam atrás de portas, como `IPaymentGateway`.
- Webhooks exigem autenticidade, idempotência e tolerância a duplicidade/reprocessamento.
- Datas persistidas representam instantes UTC de forma não ambígua; exibição e regras locais usam o `TimeZoneId` IANA configurado do tenant, conforme ADR-0012.

## Observabilidade e operação

Logging estruturado deve carregar, quando disponível, `CorrelationId`, `RequestId`, `UserId`, `TenantId` e operação, sem segredos. Health checks, tratamento global de exceções, métricas e auditoria serão introduzidos nas fases previstas pelo roadmap.

## Restrições arquiteturais

- nenhum microservice ou broker no MVP sem ADR aprovado;
- nenhum acesso cross-tenant implícito;
- Platform Admin e Tenant Admin são escopos distintos;
- Billing SaaS e pagamentos operacionais são contextos distintos;
- contratos públicos e limites de módulo só mudam mediante decisão controlada.

## Stack e organização física aprovadas na F1

- .NET 10 LTS, ASP.NET Core 10, EF Core 10 e Npgsql 10;
- Angular 22, TypeScript na faixa oficial do Angular 22 e Node.js 24 LTS;
- PrimeNG 22 como biblioteca de UI, acessada preferencialmente por imports de componente e encapsulada pela camada `shared/ui`;
- projetos backend sob `backend/src`, testes sob `backend/tests` e SPA sob `frontend/nexora-web`;
- PostgreSQL e os três processos executáveis locais orquestrados por Docker Compose.

A direção de dependências física é:

```text
Nexora.Api -> Nexora.Application + Nexora.Infrastructure
Nexora.Infrastructure -> Nexora.Application + Nexora.Domain
Nexora.Application -> Nexora.Domain
Nexora.Domain -> nenhuma camada do Nexora
```

## Decisões consolidadas no roadmap F0–F15

- Mercado Pago é o primeiro adaptador do Billing SaaS, atrás de `IPaymentGateway`;
- timezone tenant-scoped usa identificadores IANA e persistência UTC;
- a topologia Azure, CI/CD, backup e restore estão definidos na F14 e em seus runbooks, sem provisionamento automático neste checkpoint.

## Onboarding self-service (F13)

Contas globais autenticadas podem manter um rascunho user-scoped e concluir a criação do tenant em uma transação única. A conclusão cria o administrador inicial e uma assinatura `Trialing` de 14 dias, sem pagamento. Segmentos e planos são catálogos globais; somente planos ativos, públicos, elegíveis para trial e com preço ativo são oferecidos. Consulte `ADR-0016-f13-self-service-onboarding.md`.
