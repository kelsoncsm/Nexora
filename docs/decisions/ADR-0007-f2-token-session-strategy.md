# ADR-0007 — Estratégia de tokens e sessão da F2

## Contexto

A SPA precisa manter sessões revogáveis sem expor o refresh token ao JavaScript ou persistir o access token em armazenamento durável acessível por XSS.

## Decisão

Emitir JWT de acesso com duração configurável padrão de 15 minutos e refresh token aleatório com duração configurável padrão de 7 dias. O access token fica somente em memória no Angular. O refresh token é enviado em cookie `HttpOnly`, `Secure` fora de Development, `SameSite=Strict` e com path limitado aos endpoints de identidade.

Persistir somente SHA-256 do refresh token. Cada uso rotaciona o token; reutilização de token revogado invalida os tokens ativos da mesma família. Logout revoga o token apresentado. Issuer, audience, assinatura e expiração do JWT são sempre validados; a chave de assinatura é configuração externa obrigatória com no mínimo 32 caracteres.

## Alternativas

- Refresh token em local/session storage: integração simples, mas expõe a credencial de longa duração a XSS.
- Access token em cookie: reduz acesso via JavaScript, mas exige uma estratégia CSRF diferente para todas as APIs.
- Sessão exclusivamente server-side: revogação direta, mas contraria o ADR-0004 aprovado.

## Consequências

Recarregar a SPA exige refresh silencioso. Requests de cookie usam credenciais e CORS restrito à origem configurada. Produção deve fornecer HTTPS e secret forte; tokens e hashes não são registrados em logs nem retornados em URLs.

## Status

Accepted
