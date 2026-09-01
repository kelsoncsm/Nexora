# Roadmap do Nexora

Cada fase é executada, validada e aprovada isoladamente. Uma fase não antecipa comportamento da seguinte.

| Fase | Objetivo | Critério principal |
|---|---|---|
| F0 | Documentação e arquitetura | arquitetura revisável, sem código de negócio |
| F1 | Fundação da solution | backend, frontend e PostgreSQL executam localmente |
| F2 | Identity e segurança | acesso somente por usuário autenticado/autorizado |
| F3 | Multi-tenancy | isolamento automatizado entre tenants |
| F4 | Administração da plataforma | Platform Admin gerencia tenants com proteção e auditoria |
| F5 | Planos e features | acesso resolvido por configuração/plano |
| F6 | Clientes | CRUD tenant-scoped com busca e paginação |
| F7 | Profissionais e serviços | cadastros e vínculos tenant-scoped |
| F8 | Agenda | disponibilidade, conflitos, status e timezone corretos |
| F9 | Billing e assinaturas | ciclo da assinatura SaaS consistente |
| F10 | Pagamentos | gateway inicial e webhooks idempotentes |
| F11 | Notificações | abstrações e primeiro canal necessário |

> QA visual pendente não bloqueante: consultar `frontend-visual-qa.md`. Implementação, build e testes estão OK; falta inspeção interativa nos viewports definidos.
| F12 | Relatórios | métricas operacionais autorizadas e confiáveis |
| F13 | Onboarding SaaS | wizard completo de ativação |
| F14 | Hardening de produção | segurança, operação, CI/CD e recuperação validados |
| F15 | Primeiro vertical | barbearia/salão configurado sobre o Core |

## Portões globais

Cada fase deve revisar escopo, segurança, autorização, tenancy quando aplicável, testes, migrations, documentação e diff. Falha em validação obrigatória resulta em status parcial ou bloqueado. Commit e avanço de fase dependem de solicitação/aprovação do responsável.

## Fundação disponível após a F1

A F1 estabelece solution .NET, API, persistência EF Core/PostgreSQL sem entidades de domínio, testes-base, Angular strict com PrimeNG e ambiente local em Docker Compose. Não inclui autenticação nem multi-tenancy funcional.

## Pendências que exigem decisão futura

- F2/F3: parâmetros de token e mecanismo público de seleção do tenant;
- F8: representação técnica e política de timezone/agendamento;
- F9/F10: regras comerciais de assinatura e gateway do MVP;
- F14 concluída em código: Azure/Bicep, CI/CD OIDC, hardening, observabilidade, migrations controladas e runbooks. Provisionamento real permanece pendente e não bloqueante.
- F15 concluída: template orientado por configuração, segmento semeado por migration e setup inicial opcional e tenant-scoped para Barbearia/Salão.

Essas pendências devem ser resolvidas na fase apropriada e registradas em ADR quando arquiteturais.

## Implementação F11/F12

- F11: Resend atrás de `IEmailSender`, PostgreSQL Outbox, worker interno, fake, retry e WelcomeEmail. Validação externa Resend pendente e não bloqueante.
- F12: indicadores tenant e plataforma com período limitado, autorização explícita, isolamento e dashboards responsivos.
