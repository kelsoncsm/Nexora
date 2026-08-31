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
| F12 | Relatórios | métricas operacionais autorizadas e confiáveis |
| F13 | Onboarding SaaS | wizard completo de ativação |
| F14 | Hardening de produção | segurança, operação, CI/CD e recuperação validados |
| F15 | Primeiro vertical | barbearia/salão configurado sobre o Core |

## Portões globais

Cada fase deve revisar escopo, segurança, autorização, tenancy quando aplicável, testes, migrations, documentação e diff. Falha em validação obrigatória resulta em status parcial ou bloqueado. Commit e avanço de fase dependem de solicitação/aprovação do responsável.

## F0 atual

A F0 entrega README, arquitetura, tenancy, segurança, Billing, diretrizes de API, roadmap e ADRs iniciais. Não cria solution, projetos, banco, migrations, Docker, backend ou frontend.

## Pendências que exigem decisão futura

- F1: versões exatas da stack, estrutura física da solution e biblioteca de UI;
- F2/F3: parâmetros de token e mecanismo público de seleção do tenant;
- F8: representação técnica e política de timezone/agendamento;
- F9/F10: regras comerciais de assinatura e gateway do MVP;
- F14: topologia de produção, backup/restore, SLOs e CI/CD.

Essas pendências devem ser resolvidas na fase apropriada e registradas em ADR quando arquiteturais.
