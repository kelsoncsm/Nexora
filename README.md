# Nexora

Nexora é uma plataforma SaaS multi-nicho, multi-tenant e configurável para empresas de serviços.

O produto é construído como um **Modular Monolith**, com ASP.NET Core, Angular, Entity Framework Core e PostgreSQL.

## Documentação

- [Plano mestre](MASTER_PLAN.md)
- [Arquitetura](docs/architecture.md)
- [Multi-tenancy](docs/tenancy.md)
- [Segurança](docs/security.md)
- [Billing](docs/billing.md)
- [Diretrizes de API](docs/api-guidelines.md)
- [Relatórios](docs/reports.md)
- [Roadmap](docs/roadmap.md)
- [Decisões arquiteturais](docs/decisions/)

## Stack da fundação

- .NET 10 LTS / ASP.NET Core 10
- Entity Framework Core 10 / Npgsql 10
- PostgreSQL 18
- Angular 22 / TypeScript 6
- PrimeNG 22
- Node.js 24 LTS
- Docker Compose

## Estrutura

```text
backend/
  src/
    Nexora.Api/
    Nexora.Application/
    Nexora.Domain/
    Nexora.Infrastructure/
  tests/
    Nexora.UnitTests/
    Nexora.IntegrationTests/
frontend/nexora-web/
docs/
docker-compose.yml
```

Dependências do backend seguem `Api -> Application + Infrastructure`, `Infrastructure -> Application + Domain` e `Application -> Domain`. O domínio não conhece camadas externas.

## Princípios essenciais

- isolamento de tenant é uma propriedade de segurança do backend;
- módulos possuem responsabilidades e contratos explícitos;
- planos, features e nichos são configuráveis, sem forks do produto;
- Billing da plataforma não se mistura ao financeiro operacional do tenant;
- microservices não fazem parte do MVP;
- segredos nunca são versionados.

## Execução com Docker

Crie a configuração local a partir do exemplo e troque a senha placeholder:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

Serviços locais:

- frontend: `http://localhost:4200`
- API: `http://localhost:8080`
- Swagger em desenvolvimento: `http://localhost:8080/swagger`
- health geral: `http://localhost:8080/health`
- liveness: `http://localhost:8080/health/live`
- readiness/PostgreSQL: `http://localhost:8080/health/ready`
- observabilidade do outbox: `http://localhost:8080/health/observability`

O arquivo `.env` é ignorado pelo Git. O `.env.example` contém somente valores de exemplo e não deve ser reutilizado em produção.

Para provisionar o primeiro Platform Admin, cadastre o usuário normalmente e defina temporariamente `PLATFORM_ADMIN_EMAIL` no `.env`. O bootstrap é idempotente, não cria senha e fica desabilitado quando o valor está vazio. A área administrativa fica em `/admin`.

E-mail usa `FakeEmailSender` por padrão. Para smoke externo com Resend, configure `EMAIL_PROVIDER=Resend`, remetente verificado e `RESEND_API_KEY`; nunca versione essas credenciais. A ausência de domínio/credencial real não afeta a suíte automatizada.

## Execução sem Docker

Backend:

```powershell
$env:ConnectionStrings__NexoraDatabase = 'Host=localhost;Port=5432;Database=nexora;Username=nexora;Password=<senha-local>'
$env:Identity__SigningKey = '<chave-aleatoria-com-pelo-menos-32-caracteres>'
dotnet run --project backend/src/Nexora.Api
```

Frontend:

```powershell
Set-Location frontend/nexora-web
npm ci
npm start
```

## Validação

```powershell
dotnet restore backend/Nexora.slnx
dotnet build backend/Nexora.slnx --no-restore
dotnet test backend/Nexora.slnx --no-build

Set-Location frontend/nexora-web
npm ci
npm test -- --watch=false
npm run build
```

## Desenvolvimento

Consulte `AGENTS.md`, `MASTER_PLAN.md` e o prompt da fase antes de qualquer alteração. A F1 fornece apenas a fundação técnica: autenticação pertence à F2 e multi-tenancy funcional à F3.

## Produção

A produção aprovada usa Azure Static Web Apps, Container Apps, PostgreSQL Flexible Server, ACR, Key Vault/Managed Identity e Application Insights. O Bicep está em `infra/`, os workflows em `.github/workflows/` e os procedimentos em `docs/operations/`. Nenhum recurso Azure é criado automaticamente. Migrations de Production usam o bundle `/app/migrate` em job controlado e nunca executam no startup normal da API.
