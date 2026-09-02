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

## Reverse proxy e forwarded headers

A API roda atrás de um proxy reverso (ingress do Azure Container Apps). Ela depende dos headers
`X-Forwarded-For` e `X-Forwarded-Proto` para o IP real do cliente (rate limiting de `auth`, auditoria,
logs) e para o esquema (`UseHttpsRedirection`). Sem uma lista explícita de proxies confiáveis o
middleware ignora esses headers — todo o tráfego colapsa no IP do proxy (10 tentativas de login
bloqueiam a plataforma inteira) e a redireção HTTPS pode entrar em loop.

Configure **pelo menos um** dos campos abaixo por ambiente, com o valor da infraestrutura real
(nunca hardcode no repositório):

- `ReverseProxy:KnownNetworks` — lista de faixas CIDR das quais o proxy origina as requisições
  (ex.: a sub-rede de infraestrutura do Container Apps Environment). Preferível quando o IP do
  proxy é dinâmico.
- `ReverseProxy:KnownProxies` — lista de IPs fixos do proxy/load balancer.

Em `Production` o startup **falha** (`ValidateProductionConfiguration`) se ambos estiverem vazios
ou se algum valor não for um IP / CIDR válido, produzindo um diagnóstico explícito. Em
`Development` e `Testing` a validação é ignorada e o comportamento sem proxy é preservado.
