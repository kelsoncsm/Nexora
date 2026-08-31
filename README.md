# Nexora

Nexora é uma plataforma SaaS multi-nicho, multi-tenant e configurável para empresas de serviços.

O produto será construído como um **Modular Monolith**, com ASP.NET Core, Angular, Entity Framework Core e PostgreSQL. A F0 contém somente documentação e decisões arquiteturais; nenhuma aplicação foi criada ainda.

## Documentação

- [Plano mestre](MASTER_PLAN.md)
- [Arquitetura](docs/architecture.md)
- [Multi-tenancy](docs/tenancy.md)
- [Segurança](docs/security.md)
- [Billing](docs/billing.md)
- [Diretrizes de API](docs/api-guidelines.md)
- [Roadmap](docs/roadmap.md)
- [Decisões arquiteturais](docs/decisions/)

## Estado atual

Fase **F0 — Documentação e Arquitetura**. A fundação técnica será criada somente na F1, após revisão e aprovação desta documentação.

## Princípios essenciais

- isolamento de tenant é uma propriedade de segurança do backend;
- módulos possuem responsabilidades e contratos explícitos;
- planos, features e nichos são configuráveis, sem forks do produto;
- Billing da plataforma não se mistura ao financeiro operacional do tenant;
- microservices não fazem parte do MVP;
- segredos nunca são versionados.

## Desenvolvimento

Consulte `AGENTS.md`, `MASTER_PLAN.md` e o prompt da fase antes de qualquer alteração. Instruções de execução local serão adicionadas na F1, quando os projetos forem criados.
