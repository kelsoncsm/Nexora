# Relatório de hardening F14

## Controles implementados

- configuração Production fail-fast, CORS/hosts explícitos e providers reais;
- HSTS, HTTPS de plataforma, headers, cookies seguros e forwarded headers limitado a proxies conhecidos;
- rate limiting segmentado em identidade, onboarding, checkout e webhook;
- ProblemDetails sem detalhes internos e correlação estruturada;
- liveness/readiness PostgreSQL e health separado para backlog do outbox;
- OpenTelemetry para Application Insights quando configurado;
- containers multi-stage, não-root, healthcheck, runtime sem SDK e sem secrets;
- migrations fora do startup produtivo, empacotadas e executadas por job único;
- Bicep com rede privada, PITR, Managed Identity, Key Vault, ACR e tags SHA;
- CI com Release/tests/audits/IaC/container/secret scan e deploy OIDC protegido;
- runbooks de deploy, rollback, restore, rotação e incidentes.

## Custos mensais variáveis

Geram custo: Container Apps/egress, PostgreSQL Flexible Server/storage/backups, ACR, Log Analytics/Application Insights e DNS/domínio. Static Web Apps pode iniciar no Free. Não foram habilitados AKS, HA/replicas, multi-region, Redis, Service Bus ou Front Door Premium.

## Riscos restantes antes do go-live

- provisionamento Azure, domínio e certificados não executados;
- restore PITR e alertas precisam de ensaio no ambiente real;
- SKUs/região e private endpoints devem ser confirmados por custo/disponibilidade;
- remetente Resend e credenciais Mercado Pago precisam de validação externa;
- CSP `connect-src https:` deve ser restrita ao host final da API quando o domínio existir;
- SLOs ainda não possuem baseline de tráfego produtivo.

## Hardening pré-checkpoint em andamento

O ciclo iniciado após a auditoria do roadmap não representa declaração de produção pronta. Foram implementadas correções para separar Billing SaaS dos relatórios operacionais do tenant, serializar alterações concorrentes de agenda no PostgreSQL, identificar invoices por período de cobertura, consumir refresh tokens atomicamente, limitar o bearer interceptor à API Nexora e validar freshness da assinatura do webhook.

A migration aditiva `20260901200000_HardenPreCheckpointConcurrency` foi aplicada do zero após a F15, revertida até `20260901190000_SeedBarbershopSalonVertical` e reaplicada em PostgreSQL temporário real. Os testes concorrentes direcionados passaram com 10 requests de agendamento, 10 checkouts e 10 refreshes, além do cenário de assinatura ativa.

### Última atividade interrompida antes do checkpoint

Em 01/09/2026, a regressão completa pós-hardening foi iniciada novamente após o último ajuste de persistência da invoice em falha transitória do gateway. A execução foi interrompida externamente durante o build Release, depois de `Nexora.Domain` e `Nexora.Application`; portanto aquela execução específica não chegou a validar toda a suíte nem o frontend. O checkpoint deve repetir os builds/testes afetados e não deve ser descrito como hardening concluído ou `production-ready`.

Antes do checkpoint local, essa validação interrompida foi repetida: build backend Release sem avisos, 14 testes unitários, 36 testes de integração (incluindo os cenários PostgreSQL reais), 7 testes frontend e build Angular de produção passaram. O bundle inicial ficou em 596,11 kB para budget de 500 kB; o warning permanece dívida técnica não bloqueante. A auditoria consolidada pós-hardening e as validações externas/visuais continuam fora deste checkpoint intermediário.
