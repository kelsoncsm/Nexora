# ADR-0017 — Plataforma de produção Azure da F14

- Status: Aceita
- Data: 2026-09-01

## Contexto

O Nexora precisa de uma topologia produtiva reproduzível, observável e recuperável, sem transferir ao time a operação de VPS ou Kubernetes no MVP.

## Decisão

Adotar Azure Static Web Apps para o Angular estático, Azure Container Apps para a API Modular Monolith, ACR com imagem imutável por commit SHA, PostgreSQL Flexible Server privado, Key Vault e Managed Identity, Log Analytics/Application Insights com OpenTelemetry, certificados gerenciados, GitHub Actions autenticado por OIDC e Bicep.

O banco mantém PITR por 14 dias, com RPO interno alvo de 15 minutos e RTO de 4 horas. O worker de outbox permanece no processo da API, exigindo ao menos uma réplica. Migrations rodam em Container Apps Job controlado antes da ativação saudável da nova revisão. Não haverá AKS, multi-region, HA, replica, Redis ou mensageria no MVP sem nova evidência/decisão.

## Consequências

- Produção é reproduzível e versões são rastreáveis por commit, imagem e revision.
- Segredos ficam no Key Vault; workloads usam identidade gerenciada para ACR e Key Vault.
- O custo inicial inclui PostgreSQL, Container Apps, ACR, observabilidade e eventualmente Static Web Apps/domínio.
- Região e domínio são parâmetros operacionais; nenhum recurso pago é provisionado nesta fase.
- Restore, rollback e rotação passam a seguir runbooks controlados.
