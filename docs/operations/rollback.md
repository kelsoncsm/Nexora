# Rollback

1. Identifique commit SHA, imagem e revision anterior no registro da release.
2. Leia a migration da release e confirme compatibilidade backward. Não volte a aplicação se o schema novo for incompatível.
3. Se compatível, reative/deploy a imagem SHA anterior no Container Apps e valide readiness/smoke.
4. Se incompatível, faça roll-forward corretivo. Down migration em produção exige aprovação e backup/restore point validado.
5. Para frontend, publique novamente o artefato do SHA anterior e confirme `config.js`.
6. Documente causa, duração, impacto, decisão e ações preventivas.

Rollback de imagem não desfaz automaticamente banco, pagamentos, mensagens ou efeitos externos.
