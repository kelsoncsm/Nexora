# Infraestrutura Azure do Nexora

O Bicep prepara Resource Group, VNet privada, PostgreSQL Flexible Server, ACR, identidade gerenciada, Key Vault RBAC, Container Apps, Static Web Apps, Log Analytics, Application Insights e alertas mínimos. Nenhum recurso é criado apenas por manter estes arquivos no repositório.

Antes do deploy, defina região/SKUs, configure a identidade OIDC do GitHub, grave os secrets operacionais no Key Vault e valide `az deployment sub what-if`. O PostgreSQL usa rede privada, PITR de 14 dias, SKU Burstable e sem HA/replica/multi-region para controlar custo. A API mantém 1–3 réplicas porque o worker de outbox está no mesmo processo.

Os secrets esperados no Key Vault são `nexora-db-connection`, `nexora-jwt-signing-key`, `nexora-mercadopago-access-token`, `nexora-mercadopago-webhook-secret` e `nexora-resend-api-key`. O template contém somente referências, nunca seus valores. O primeiro deploy usa `deployWorkloads=false`; depois que um operador autorizado grava esses secrets, execute novamente com `deployWorkloads=true`.
