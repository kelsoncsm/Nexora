# Deployment de produção

## Pré-requisitos

- commit revisado e identificado, nunca working tree local;
- GitHub Environment `production` com aprovação e variáveis descritas no workflow;
- identidade federada OIDC com mínimo acesso ao Resource Group/ACR/SWA;
- secrets do aplicativo presentes no Key Vault;
- domínio do Resend verificado e domínios/TLS Azure configurados;
- revisão do `what-if` Bicep e do custo da região/SKUs.

## Primeiro provisionamento

1. Execute `az deployment sub what-if` com `deployWorkloads=false`.
2. Provisione a fundação e confirme VNet privada, PostgreSQL, ACR, Key Vault e observabilidade.
3. Grave os secrets no Key Vault por canal autorizado. A connection string deve exigir TLS (`SSL Mode=Require`).
4. Execute novamente com `deployWorkloads=true` e uma imagem SHA existente no ACR.
5. Configure `app.<domínio>` e `api.<domínio>` com certificados gerenciados e CORS explícito.

## Release normal

O workflow manual valida a confirmação, autentica via OIDC, publica `nexora-api:<commit-sha>`, atualiza IaC, executa o migration job uma vez, ativa/verifica a revisão e publica o Angular com configuração pública da URL da API. Confirme `/health/live`, `/health/ready`, login, onboarding e webhook autenticado. Registre SHA, revision e migration.

Nunca execute migrations no startup de Production ou faça deploy a partir de `latest`.
