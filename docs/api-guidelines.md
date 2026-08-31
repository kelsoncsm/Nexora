# Diretrizes de API

## Princípios

As APIs do Nexora serão REST sobre HTTPS, orientadas a recursos, versionadas e protegidas no backend. Contratos HTTP usam DTOs; entidades do EF Core nunca são expostas diretamente.

## Rotas e nomes

- Prefixo padrão: `/api/v1/...`.
- Recursos em substantivos plurais e nomes consistentes.
- Endpoints de plataforma ficam explicitamente separados dos endpoints de tenant.
- O contrato não aceita `TenantId` como autoridade de uma operação autenticada tenant-scoped.
- Breaking changes exigem avaliação e estratégia de versionamento.

## Métodos e status

| Operação | Resposta típica |
|---|---|
| criação | `201 Created`, com localização quando aplicável |
| leitura/alteração | `200 OK` |
| remoção sem corpo | `204 No Content` |
| entrada inválida | `400 Bad Request` |
| não autenticado | `401 Unauthorized` |
| sem autorização | `403 Forbidden` |
| não encontrado | `404 Not Found` |
| conflito de estado/unicidade | `409 Conflict` |
| regra de negócio | `422 Unprocessable Content`, se adotado consistentemente |
| limite excedido | `429 Too Many Requests` |
| falha inesperada | `500 Internal Server Error` |

Recursos de outro tenant não devem ser confirmados por diferenças observáveis; a política detalhada de `403` versus `404` será uniforme e testada.

## Erros

Erros seguirão um contrato consistente compatível com Problem Details, contendo código estável, título, status, detalhe seguro, identificador de correlação e erros de validação quando aplicável. Stack traces, SQL, secrets e detalhes internos não são retornados em produção.

## Listagens

Listagens potencialmente grandes são paginadas. Parâmetros de página/tamanho têm defaults e limites configurados; filtros e ordenação usam campos permitidos, documentados e validados. A resposta informa os metadados necessários sem executar contagens caras por padrão sem justificativa.

## Concorrência, idempotência e tempo

- Operações sensíveis a concorrência devem detectar conflito e responder consistentemente.
- Webhooks e comandos financeiros usam chave/identificador idempotente.
- Instantes trafegam em ISO 8601 com offset ou UTC de forma não ambígua.
- Regras e apresentação no horário local usam o timezone configurado do tenant.

## Segurança

Toda operação define requisitos de autenticação, permissão, escopo e feature. IDs são tratados como entrada não confiável e validados contra o tenant atual. Aplicam-se limites de request, validação server-side, CORS restritivo, rate limiting quando aplicável e logging sem dados sensíveis.

## Documentação e evolução

OpenAPI será habilitado na F1 e refletirá contratos, autenticação, respostas e erros. Mudanças públicas relevantes devem atualizar documentação e testes de contrato. Políticas detalhadas de paginação, Problem Details e versionamento serão materializadas na fundação técnica sem contrariar estas diretrizes.
