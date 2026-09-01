# Restore do PostgreSQL

Objetivos internos do MVP: RPO ≤ 15 minutos, RTO ≤ 4 horas e PITR de 14 dias.

1. Declare incidente, congele deploys/migrations e determine timestamp anterior ao dano.
2. No PostgreSQL Flexible Server, inicie point-in-time restore para um novo servidor; nunca sobrescreva o servidor sob investigação.
3. Mantenha rede privada, TLS e identidade/usuário dedicado equivalentes.
4. Compare `__EFMigrationsHistory`, integridade, contagens críticas e isolamento de tenant.
5. Execute smoke tests sem enviar e-mail/cobrança real.
6. Grave nova connection string no Key Vault, gere nova revisão e valide readiness.
7. Troque tráfego somente após aprovação. Preserve o servidor anterior para análise conforme retenção.
8. Registre RPO/RTO reais e execute post-mortem.

Teste este procedimento periodicamente em ambiente descartável. Backup não testado não comprova recuperabilidade.
