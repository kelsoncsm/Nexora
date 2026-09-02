# Nexora — MASTER PLAN
## SaaS Multi-Nicho, Multi-Tenant e Configurável

**Versão:** 1.0  
**Data:** 31/08/2026  
**Status:** Planejamento inicial

---

# 1. Visão do produto

O Nexora será uma plataforma SaaS multi-tenant para gestão de empresas de serviços de diferentes nichos.

A plataforma deve permitir que uma mesma base tecnológica atenda, por exemplo:

- Barbearias
- Salões de beleza
- Manicures
- Clínicas
- Consultórios
- Estúdios de estética
- Academias
- Oficinas
- Outros negócios de serviços

O princípio arquitetural é:

> O núcleo do sistema é compartilhado; comportamento, módulos, limites e configurações são determinados por tenant, segmento, plano e funcionalidades.

O primeiro vertical recomendado para validação comercial é **barbearia/salão**, por ser relativamente simples e ter forte aderência a agenda, clientes, profissionais, serviços e pagamentos.

---

# 2. Princípios

1. Multi-tenancy desde o primeiro commit.
2. Segurança no backend; o frontend nunca é autoridade.
3. Modular Monolith inicialmente.
4. Clean Architecture.
5. DDD pragmático.
6. SOLID.
7. Código testável.
8. PostgreSQL como banco principal.
9. Angular como frontend principal.
10. APIs REST versionadas.
11. Observabilidade desde cedo.
12. Nenhum segredo no código-fonte.
13. Planos e features configuráveis pelo Platform Admin.
14. Evitar regras de negócio específicas de um único nicho no Core.
15. Não começar com microservices.
16. Cada fase deve ser pequena, testável e com critérios de aceite claros.
17. Nenhuma fase deve quebrar funcionalidades já aprovadas.
18. Alterações fora do escopo da fase devem ser evitadas.

---

# 3. Stack recomendada

## Backend

- C#
- ASP.NET Core
- .NET LTS vigente no momento da implementação
- Entity Framework Core
- PostgreSQL
- FluentValidation ou mecanismo equivalente
- OpenAPI/Swagger
- Serilog ou logging estruturado equivalente
- JWT + Refresh Token
- Testes unitários
- Testes de integração

## Frontend

- Angular
- TypeScript
- Angular Router
- Reactive Forms
- HTTP Interceptors
- Guards
- Componentes reutilizáveis
- Biblioteca de UI escolhida no início do projeto

## Infraestrutura

- Docker
- Docker Compose para desenvolvimento
- PostgreSQL
- CI/CD
- HTTPS em produção
- Secret management adequado ao ambiente

---

# 4. Arquitetura

## 4.1 Visão

```text
                    INTERNET
                       |
                 Reverse Proxy
                       |
                Angular Frontend
                       |
                    HTTPS
                       |
                 .NET Web API
                       |
       +---------------+----------------+
       |               |                |
    Identity        Tenancy          Modules
       |               |                |
       +---------------+----------------+
                       |
                  Application
                       |
                    Domain
                       |
                 Infrastructure
                       |
                   PostgreSQL
```

## 4.2 Modular Monolith

A aplicação será inicialmente um único deploy lógico, porém organizada por módulos e bounded contexts.

Módulos principais:

```text
Identity
Tenancy
Administration
Plans
Features
Billing
Customers
Professionals
Services
Scheduling
Payments
Notifications
Reports
```

Quando existir necessidade real de escala ou isolamento, módulos poderão ser extraídos para serviços independentes.

---

# 5. Estrutura de diretórios

```text
nexora/
├── docs/
│   ├── architecture.md
│   ├── roadmap.md
│   ├── security.md
│   ├── tenancy.md
│   ├── billing.md
│   ├── api-guidelines.md
│   └── decisions/
│
├── backend/
│   ├── src/
│   │   ├── Nexora.Api/
│   │   ├── Nexora.Application/
│   │   ├── Nexora.Domain/
│   │   ├── Nexora.Infrastructure/
│   │   └── Modules/
│   │       ├── Identity/
│   │       ├── Tenancy/
│   │       ├── Administration/
│   │       ├── Plans/
│   │       ├── Features/
│   │       ├── Billing/
│   │       ├── Customers/
│   │       ├── Professionals/
│   │       ├── Services/
│   │       ├── Scheduling/
│   │       ├── Payments/
│   │       ├── Notifications/
│   │       └── Reports/
│   ├── tests/
│   │   ├── Nexora.UnitTests/
│   │   └── Nexora.IntegrationTests/
│   ├── Dockerfile
│   └── Nexora.slnx
│
├── frontend/
│   └── nexora-web/
│
├── database/
├── docker/
├── scripts/
├── docker-compose.yml
├── .gitignore
├── README.md
└── LICENSE
```

A estrutura pode ser adaptada durante a F1, desde que a decisão seja documentada.

---

# 6. Modelo de tenancy

## 6.1 Conceito

Um Tenant representa uma empresa/conta cliente.

Exemplo:

```text
Tenant A
Barbearia João

Tenant B
Studio Ana

Tenant C
Clínica Saúde+
```

Todos compartilham a aplicação, mas seus dados devem permanecer isolados.

## 6.2 Estratégia inicial

Usar:

> Banco compartilhado + tabelas compartilhadas + TenantId.

Exemplo:

```text
Customer
---------
Id
TenantId
Name
Phone
Email
CreatedAt
```

## 6.3 Regra crítica

O backend deve determinar o TenantId pelo contexto autenticado.

Não confiar em:

```text
TenantId enviado pelo Angular
```

quando a operação puder ser derivada do usuário autenticado.

## 6.4 Isolamento

Toda consulta e comando que manipule dados tenant-scoped deve respeitar o contexto do tenant.

Criar abstração:

```text
ITenantContext
```

Responsável por disponibilizar o tenant atual.

Criar mecanismos de proteção para evitar consultas sem filtro de tenant.

---

# 7. Identidade e autorização

## 7.1 Entidades

```text
User
Tenant
TenantUser
Role
Permission
RolePermission
RefreshToken
```

Um usuário poderá pertencer a mais de um tenant.

Exemplo:

```text
User: João

Tenant A -> Owner
Tenant B -> Manager
```

## 7.2 RBAC

Permissões devem ser granulares.

Exemplos:

```text
customers.read
customers.create
customers.update
customers.delete

services.read
services.create
services.update
services.delete

appointments.read
appointments.create
appointments.update
appointments.cancel

financial.read
financial.create
financial.update

reports.read
```

## 7.3 Platform Admin

O administrador da plataforma não deve ser simplesmente um usuário comum com um booleano espalhado pelo código.

Criar uma estratégia explícita para distinguir:

```text
Platform Scope
Tenant Scope
```

e proteger endpoints administrativos.

---

# 8. Segmentos

Criar entidade/configuração:

```text
BusinessSegment
```

Exemplos:

```text
BARBERSHOP
BEAUTY_SALON
MANICURE
CLINIC
CONSULTING
GYM
WORKSHOP
OTHER
```

O segmento não deve gerar condicionais espalhados pelo domínio.

Evitar:

```csharp
if (segment == "BARBERSHOP")
{
    ...
}
```

Preferir configuração, capabilities, módulos e regras específicas bem encapsuladas.

---

# 9. Features

Criar catálogo de funcionalidades.

Exemplos:

```text
AGENDA
CUSTOMERS
PROFESSIONALS
SERVICES
FINANCE
PAYMENTS
WHATSAPP
CRM
REPORTS
STOCK
LOYALTY
COUPONS
```

Modelo conceitual:

```text
Feature
PlanFeature
TenantFeatureOverride
```

A resolução de acesso pode considerar:

```text
Tenant
+
Plan
+
Feature
+
Override
```

---

# 10. Planos

Entidades:

```text
Plan
PlanFeature
Subscription
SubscriptionEvent
BillingPayment
```

Exemplo:

```text
Plano Básico
R$ 49,90

Agenda: SIM
Clientes: SIM
Financeiro: NÃO
WhatsApp: NÃO

Limite profissionais: 3
Limite clientes: 300
```

Plano Premium:

```text
R$ 149,90

Agenda: SIM
Clientes: SIM
Financeiro: SIM
WhatsApp: SIM
CRM: SIM
Relatórios: SIM

Limite profissionais: 20
Limite clientes: 5000
```

Os limites não devem ficar hardcoded.

---

# 11. Billing

Separar claramente:

## Billing da plataforma

```text
Tenant
 -> Subscription
 -> Plan
 -> Invoice/Charge
 -> BillingPayment
```

## Pagamentos do negócio

```text
Customer
 -> Appointment/Order
 -> Payment
```

São domínios diferentes.

Não misturar pagamento da assinatura do SaaS com pagamento de uma venda/agendamento do tenant.

---

# 12. Gateway de pagamento

Criar uma abstração:

```text
IPaymentGateway
```

Com possibilidade de adaptadores:

```text
MercadoPagoGateway
StripeGateway
AsaasGateway
PagarmeGateway
```

A implementação inicial deve utilizar apenas um gateway escolhido para o MVP.

Não criar integrações com todos os gateways na primeira versão.

---

# 13. Domínio operacional

## Customer

```text
Id
TenantId
Name
Phone
Email
BirthDate
Notes
Status
CreatedAt
UpdatedAt
```

## Professional

```text
Id
TenantId
Name
Email
Phone
Status
```

## Service

```text
Id
TenantId
Name
Description
DurationMinutes
Price
Status
```

## ProfessionalService

Relaciona profissionais e serviços.

## WorkingHours

Define disponibilidade recorrente.

## Appointment

```text
Id
TenantId
CustomerId
ProfessionalId
ServiceId
StartAt
EndAt
Status
Notes
CreatedAt
UpdatedAt
```

Status:

```text
Scheduled
Confirmed
InProgress
Completed
Cancelled
NoShow
```

---

# 14. Agenda

A agenda deve considerar:

- horário de funcionamento;
- horário do profissional;
- folgas;
- bloqueios;
- duração do serviço;
- conflitos;
- status do agendamento;
- timezone do tenant.

Nunca assumir timezone global fixo.

O tenant deve possuir configuração de timezone.

---

# 15. Onboarding

Fluxo recomendado:

```text
Cadastro
   |
Criação do Tenant
   |
Escolha do segmento
   |
Escolha do plano
   |
Dados da empresa
   |
Primeiro usuário
   |
Serviços
   |
Profissionais
   |
Horários
   |
Dashboard
```

No futuro, IA poderá ajudar a montar configurações iniciais.

---

# 16. Painel Platform Admin

Menu inicial:

```text
Dashboard
Tenants
Users
Segments
Plans
Features
Subscriptions
Payments
Coupons
System Settings
Audit Logs
System Logs
```

Dashboard:

```text
Total de tenants
Tenants ativos
Tenants em trial
Assinaturas ativas
Assinaturas inadimplentes
MRR
Churn
Novos tenants
```

Métricas comerciais devem ser adicionadas somente quando houver dados confiáveis.

---

# 17. Painel Tenant

Menu:

```text
Dashboard
Agenda
Clientes
Profissionais
Serviços
Financeiro
Relatórios
Configurações
Usuários
Assinatura
```

O menu deve ser filtrado pelas permissões/features do tenant.

---

# 18. Segurança

Requisitos mínimos:

- HTTPS em produção.
- Hash seguro de senha.
- JWT de curta duração.
- Refresh Token seguro e revogável.
- Controle de acesso server-side.
- RBAC.
- Tenant isolation.
- Validação de entrada.
- Rate limiting.
- CORS restritivo.
- Headers de segurança.
- Proteção contra IDOR.
- Logs de segurança.
- Auditoria para ações críticas.
- Secrets fora do repositório.
- Política de senha.
- Proteção de endpoints administrativos.
- Não registrar tokens/senhas em logs.
- Sanitização/validação de dados.
- Controle de tamanho de requests.
- Migrações versionadas.
- Backup e restore planejados.

---

# 19. Observabilidade

Implementar:

```text
Structured Logging
CorrelationId
RequestId
Health Checks
Exception Handling
Metrics
Audit Logs
```

Endpoints sugeridos:

```text
/health
/health/ready
/health/live
```

---

# 20. API

Usar versionamento.

Exemplo:

```text
/api/v1/auth
/api/v1/tenants
/api/v1/customers
/api/v1/professionals
/api/v1/services
/api/v1/appointments
/api/v1/billing
```

Padronizar:

- respostas;
- erros;
- paginação;
- filtros;
- ordenação;
- validação;
- códigos HTTP.

Nunca expor entidades EF diretamente.

Usar DTOs/Contracts.

---

# 21. Banco de dados

Tabelas conceituais iniciais:

```text
users
refresh_tokens
tenants
tenant_users
roles
permissions
role_permissions

business_segments

features
plans
plan_features
tenant_feature_overrides

subscriptions
subscription_events
billing_payments

customers
professionals
services
professional_services

working_hours
blocked_periods
appointments

payments
notifications

audit_logs
```

Adicionar índices principalmente para:

```text
TenantId
TenantId + Status
TenantId + CreatedAt
TenantId + StartAt
TenantId + CustomerId
TenantId + ProfessionalId
```

Os índices reais devem ser definidos conforme consultas reais.

---

# 22. Testes

## Unitários

Testar regras de domínio:

- conflito de agenda;
- cálculo de duração;
- transição de status;
- limites de plano;
- autorização;
- regras de assinatura.

## Integração

Testar:

- PostgreSQL;
- autenticação;
- autorização;
- tenancy;
- migrations;
- endpoints;
- filtros;
- isolamento de tenant.

## E2E

Fluxo mínimo:

```text
Criar conta
 -> Criar tenant
 -> Login
 -> Configurar empresa
 -> Criar profissional
 -> Criar serviço
 -> Criar cliente
 -> Criar agendamento
 -> Consultar agenda
```

Teste obrigatório:

```text
Tenant A NÃO consegue consultar dados do Tenant B.
```

---

# 23. Roadmap

## F0 — Architecture & Documentation

Objetivo:

Criar documentação base e decisões arquiteturais.

Entregáveis:

```text
README
architecture.md
roadmap.md
security.md
tenancy.md
billing.md
ADR inicial
```

Critério:

Arquitetura revisável antes da implementação.

---

## F1 — Solution Foundation

Criar:

- solução .NET;
- projetos;
- Angular;
- PostgreSQL;
- Docker Compose;
- health check;
- Swagger/OpenAPI;
- logging;
- configuração por ambiente;
- testes base.

Critério:

Backend, frontend e banco executam localmente.

---

## F2 — Identity & Security

Criar:

- cadastro;
- login;
- hash de senha;
- JWT;
- refresh token;
- logout/revogação;
- autorização;
- roles;
- permissions.

Critério:

Usuário autenticado consegue acessar somente recursos autorizados.

---

## F3 — Multi-Tenancy

Criar:

- Tenant;
- TenantUser;
- TenantContext;
- resolução de tenant;
- isolamento de consultas;
- proteção contra IDOR;
- testes de isolamento.

Critério:

Nenhum tenant consegue acessar dados de outro tenant.

---

## F4 — Platform Administration

Criar:

- Platform Admin;
- tenants;
- usuários;
- segmentos;
- dashboard administrativo;
- auditoria.

Critério:

Administrador da plataforma consegue gerenciar tenants sem quebrar isolamento.

---

## F5 — Plans & Features

Criar:

- Feature;
- Plan;
- PlanFeature;
- limites;
- overrides;
- resolução de feature;
- enforcement em runtime (endpoint filter + limites nos serviços de criação) — ver ADR-0019.

Critério:

O acesso aos módulos é controlado por plano/configuração. **Atendido em runtime** (ADR-0019): módulos gateados por `RequireFeature`, limites verificados na criação, feature ausente → 403 `feature_not_in_plan`, limite → 409 `plan_limit_reached`.

---

## F6 — Customers

Criar:

- CRUD;
- busca;
- paginação;
- filtros;
- histórico;
- contatos.

Critério:

Cliente é sempre isolado pelo tenant.

---

## F7 — Professionals & Services

Criar:

- profissionais;
- serviços;
- vínculo profissional/serviço;
- preços;
- duração;
- disponibilidade.

---

## F8 — Scheduling

Criar:

- agenda;
- working hours;
- bloqueios;
- appointments;
- conflitos;
- status;
- timezone.

Critério:

Não permitir agendamentos conflitantes conforme as regras definidas.

---

## F9 — Billing & Subscriptions

Criar:

- integração com os planos definidos na F5;
- assinatura;
- trial;
- renovação;
- estados da assinatura;
- inadimplência;
- histórico.

Estados possíveis:

```text
Trialing
Active
PastDue
Cancelled
Expired
```

---

## F10 — Payments

Criar:

- abstração de gateway;
- primeiro gateway;
- checkout;
- webhook;
- idempotência;
- reconciliação;
- logs.

Critério:

Webhooks devem ser idempotentes.

---

## F11 — Notifications

Criar abstrações:

```text
IEmailSender
ISmsSender
IWhatsAppSender
```

Começar por e-mail se necessário.

---

## F12 — Reports

Criar:

- agenda;
- clientes;
- faturamento;
- produtividade;
- métricas por período.

---

## F13 — SaaS Onboarding

Criar wizard:

```text
Conta
Empresa
Segmento
Plano
Serviços
Profissionais
Horários
Conclusão
```

---

## F14 — Production Hardening

Revisar:

- segurança;
- logs;
- rate limiting;
- backups;
- migrations;
- performance;
- observabilidade;
- CI/CD;
- documentação;
- disaster recovery.

---

## F15 — First Vertical

Primeiro vertical:

> Barbearia / Salão

Criar templates/configurações:

- serviços comuns;
- campos;
- dashboard;
- onboarding;
- textos;
- configurações;
- experiência específica.

Atenção:

O vertical deve utilizar o Core. Evitar criar um segundo sistema.

---

# 24. Estratégia de desenvolvimento com IA

Não solicitar:

> "Construa o SaaS inteiro."

Executar uma fase por vez.

Fluxo:

```text
MASTER_PLAN
    |
    v
Prompt F0
    |
Implementação
    |
Testes
    |
Review
    |
Commit
    |
Prompt F1
    |
...
```

A IA deve:

1. Ler o MASTER_PLAN.
2. Ler o roadmap.
3. Identificar a fase atual.
4. Inspecionar o repositório.
5. Não assumir arquivos inexistentes.
6. Não sobrescrever trabalho fora do escopo.
7. Implementar somente a fase solicitada.
8. Criar testes.
9. Executar testes.
10. Corrigir problemas.
11. Informar arquivos alterados.
12. Informar comandos executados.
13. Informar pendências.
14. Não declarar sucesso sem evidência.

---

# 25. Prompt mestre para o agente de desenvolvimento

Use este prompt como contexto inicial:

```text
Você é o engenheiro principal responsável pelo projeto Nexora.

Leia o arquivo MASTER_PLAN.md antes de realizar qualquer alteração.

OBJETIVO
Construir uma plataforma SaaS multi-nicho, multi-tenant, segura e configurável.

STACK
Backend: C# / ASP.NET Core / Entity Framework Core / PostgreSQL.
Frontend: Angular / TypeScript.
Infra: Docker e Docker Compose.

PRINCÍPIOS
- Clean Architecture.
- DDD pragmático.
- SOLID.
- Segurança server-side.
- Multi-tenancy desde o início.
- Modular Monolith.
- Código testável.
- Não criar microservices sem necessidade.
- Não duplicar regras por nicho.
- Não hardcodar planos/features.
- Não confiar no frontend para autorização.

REGRAS
1. Leia primeiro o MASTER_PLAN.md.
2. Identifique a fase solicitada.
3. Inspecione o repositório antes de editar.
4. Preserve alterações existentes fora do escopo.
5. Não apague arquivos sem justificativa.
6. Não altere contratos públicos sem avaliar impacto.
7. Toda regra importante deve possuir teste.
8. Toda alteração de banco deve usar migration.
9. Toda funcionalidade tenant-scoped deve respeitar TenantId.
10. Nunca aceite TenantId do cliente como autoridade de segurança.
11. Não coloque secrets no código.
12. Não implemente funcionalidades futuras antecipadamente.
13. Ao encontrar uma decisão arquitetural relevante, documente um ADR.
14. Execute os testes antes de concluir.
15. Se algum teste não puder ser executado, informe exatamente o motivo.
16. Não diga "está funcionando" sem executar uma validação compatível.

FORMATO FINAL DA EXECUÇÃO
- Resumo
- Arquivos criados
- Arquivos alterados
- Banco/migrations
- Testes executados
- Resultado dos testes
- Decisões
- Pendências
- Próximo passo recomendado
```

---

# 26. Prompt padrão de cada fase

```text
Leia MASTER_PLAN.md.

Implemente SOMENTE a fase:

[F_X — NOME DA FASE]

Antes de alterar qualquer arquivo:

1. Inspecione a estrutura atual.
2. Verifique o que já foi implementado.
3. Identifique possíveis conflitos.
4. Explique brevemente o plano de implementação.

Durante a implementação:

- siga Clean Architecture;
- aplique SOLID;
- mantenha o código testável;
- respeite multi-tenancy;
- não implemente funcionalidades de fases futuras;
- preserve alterações existentes;
- não hardcode regras que deveriam ser configuração;
- não coloque secrets no código.

Banco:

- crie migrations quando necessário;
- não altere banco manualmente sem migration;
- crie índices somente quando justificáveis.

Testes:

- crie testes unitários para regras;
- crie testes de integração para endpoints/repositórios quando aplicável;
- execute os testes.

Ao finalizar:

1. Liste arquivos criados.
2. Liste arquivos alterados.
3. Liste migrations.
4. Liste testes executados.
5. Informe o resultado.
6. Informe qualquer limitação.
7. Não avance para a próxima fase.

Critérios de aceite da fase:
[COLAR OS CRITÉRIOS DA FASE AQUI]
```

---

# 27. Critérios globais de qualidade

Antes de considerar o MVP pronto:

```text
[ ] Build backend
[ ] Build frontend
[ ] Unit tests
[ ] Integration tests
[ ] E2E tests
[ ] Database migrations
[ ] Tenant isolation
[ ] Authentication
[ ] Authorization
[ ] Error handling
[ ] Logging
[ ] Health checks
[ ] Rate limiting
[ ] Secrets management
[ ] API documentation
[ ] Docker
[ ] CI/CD
[ ] Backup strategy
[ ] Audit logs
[ ] Security review
```

---

# 28. Decisões que devem ser evitadas

## Não fazer inicialmente

- Microservices.
- Kubernetes.
- Event-driven architecture complexa.
- Marketplace.
- IA generativa em todos os módulos.
- Dez gateways de pagamento.
- Aplicativo mobile nativo.
- Domínio customizado antes do MVP.
- White-label completo.
- Sistema de estoque complexo.

Primeiro validar:

```text
Tenant
+
Usuário
+
Plano
+
Feature
+
Cliente
+
Profissional
+
Serviço
+
Agenda
+
Assinatura
```

---

# 29. Evolução futura

Depois do MVP:

```text
WhatsApp
CRM
Marketing
Fidelidade
Cupons
Estoque
Marketplace
Aplicativo mobile
Domínios personalizados
White-label
IA
Automação
Integrações
```

Possível evolução arquitetural:

```text
Modular Monolith
      |
      +--> Billing Service
      |
      +--> Notification Service
      |
      +--> Payment Service
      |
      +--> Reporting Service
```

A extração deve acontecer por necessidade real, não por antecipação.

---

# 30. Modelo de ADR

Criar arquivos:

```text
docs/decisions/ADR-0001-modular-monolith.md
docs/decisions/ADR-0002-multi-tenancy.md
docs/decisions/ADR-0003-postgresql.md
docs/decisions/ADR-0004-authentication.md
```

Modelo:

```text
# ADR-XXXX — Título

## Contexto

Qual problema estamos resolvendo?

## Decisão

Qual solução foi escolhida?

## Alternativas

Quais alternativas foram consideradas?

## Consequências

Quais são os benefícios e custos?

## Status

Accepted / Proposed / Superseded
```

---

# 31. MVP comercial

O primeiro produto vendável deve ser pequeno.

### Plano Básico

```text
Agenda
Clientes
Serviços
Profissionais
Até 3 profissionais
Até 300 clientes
```

### Plano Profissional

```text
Tudo do Básico
Financeiro
Relatórios
Até 10 profissionais
Até 2.000 clientes
```

### Plano Premium

```text
Tudo do Profissional
WhatsApp
CRM
Mais limites
```

Os preços são apenas placeholders e devem ser definidos após validação de mercado e custos.

---

# 32. Métricas SaaS

Após existir base de usuários:

```text
MRR
ARR
CAC
LTV
Churn
Activation Rate
Conversion Rate
Trial Conversion
ARPU
Retention
```

Não implementar métricas sofisticadas antes de existir dados suficientes.

---

# 33. Checklist de lançamento

## Técnico

```text
[ ] Produção configurada
[ ] HTTPS
[ ] Banco protegido
[ ] Backup
[ ] Restore testado
[ ] Logs
[ ] Monitoramento
[ ] Alertas
[ ] CI/CD
[ ] Rate limiting
[ ] Secrets
[ ] CORS
[ ] Segurança revisada
```

## Produto

```text
[ ] Landing page
[ ] Cadastro
[ ] Trial
[ ] Onboarding
[ ] Planos
[ ] Assinatura
[ ] Cancelamento
[ ] Suporte
[ ] Termos
[ ] Política de privacidade
```

## Operação

```text
[ ] Atendimento
[ ] Recuperação de conta
[ ] Processo de cobrança
[ ] Processo de cancelamento
[ ] Tratamento de inadimplência
[ ] Monitoramento de erros
```

---

# 34. Regra de ouro do projeto

O Nexora deve permitir:

```text
NOVO NICHO
    |
    v
Configuração
    +
Features
    +
Templates
    +
Campos específicos
```

e não:

```text
NOVO NICHO
    |
    v
Fork do sistema
    |
    v
Novo código
    |
    v
Nova manutenção
```

Se adicionar um novo nicho exigir copiar grande parte do sistema, a arquitetura está falhando.

---

# 35. Primeira implementação recomendada

Começar exatamente nesta ordem:

```text
F0
Arquitetura/documentação

F1
Fundação técnica

F2
Identity/Security

F3
Multi-Tenancy

F4
Platform Admin

F5
Plans/Features

F6
Customers

F7
Professionals/Services

F8
Scheduling

F9
Billing

F10
Payments

F11
Notifications

F12
Reports

F13
Onboarding

F14
Production Hardening

F15
Barbearia/Salão
```

Depois disso, o produto estará em uma posição muito melhor para receber novos nichos.

---

# 36. Próximo comando

Após criar o projeto e este arquivo, a primeira solicitação ao agente deve ser:

```text
Leia MASTER_PLAN.md.

Estamos iniciando a F0.

Não implemente código de negócio ainda.

Crie/revise somente a documentação e as decisões arquiteturais previstas para a F0.

Ao finalizar, execute uma revisão de consistência do MASTER_PLAN.md e informe qualquer conflito ou decisão que precise ser tomada antes da F1.
```
