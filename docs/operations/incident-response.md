# Resposta a incidentes

## Triage

1. Classifique impacto, tenants afetados e risco a dados/pagamentos.
2. Correlacione Application Insights, Log Analytics, revision, commit SHA, CorrelationId, TenantId e eventos de auditoria.
3. Preserve evidências e não copie tokens, secrets ou payloads sensíveis.

## Ações

- API indisponível/readiness: verifique revision, replicas e PostgreSQL.
- 5xx/exceptions: identifique endpoint/dependência e considere rollback compatível.
- outbox: consulte `/health/observability`, backlog, claims vencidos e Resend; não derrube readiness.
- webhook/billing: suspenda reprocessamentos manuais até validar assinatura, idempotência e estado financeiro.
- suspeita de secret: rotacione pelo runbook e revise logs/acessos.

## Encerramento

Registre timeline, impacto, causa raiz, mitigação, recuperação, RPO/RTO observados e ações com responsáveis. SLOs internos: disponibilidade 99,5%, endpoints comuns p95 < 1 s, erro server-side < 1% e 95% dos e-mails em até 5 minutos com provider saudável.
