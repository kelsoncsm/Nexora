# Multi-tenancy

## Estratégia aprovada

O Nexora adotará inicialmente **banco compartilhado, tabelas compartilhadas e `TenantId`** para dados pertencentes a empresas. `Tenant` representa a conta/empresa cliente. Um usuário pode participar de mais de um tenant por meio de `TenantUser`.

## Invariante de segurança

> Tenant A nunca pode ler, alterar ou excluir dados do Tenant B.

O tenant efetivo é resolvido no backend a partir da identidade autenticada, da associação ativa do usuário e do contexto explícito da requisição. Um `TenantId` vindo do frontend nunca é autoridade. Uma abstração equivalente a `ITenantContext` disponibilizará o tenant validado às operações tenant-scoped.

## Resolução de contexto

Uma requisição tenant-scoped deverá:

1. autenticar o usuário;
2. quando houver rota pública tenant-scoped, resolver o tenant pelo `TenantSlug` normalizado presente em `/t/{tenantSlug}/...`;
3. confirmar no backend que o usuário pertence ao tenant e está ativo;
4. construir o contexto imutável da requisição;
5. aplicar autorização e filtros usando esse contexto.

O `TenantSlug` é um identificador público de resolução, não uma autoridade de segurança. Fluxos não autenticados podem usá-lo para localizar tenants ativos. Após autenticação, o tenant efetivo vem exclusivamente da claim `tenant_id` emitida pelo servidor depois da validação da associação ativa. Slug ou `TenantId` enviados pelo frontend nunca autorizam acesso. Tenants inexistentes, inativos ou indisponíveis não estabelecem um `ITenantContext` válido.

A resolução pública permanece desacoplada do isolamento do domínio para permitir futuramente subdomínios ou domínios customizados. Essas estratégias não fazem parte da F3.

## Leitura e escrita

- Toda query tenant-scoped inclui o tenant atual, inclusive busca por ID.
- Criações recebem o `TenantId` do contexto autenticado, não do DTO.
- Updates e deletes verificam que o recurso pertence ao tenant atual.
- Relacionamentos validam que todas as entidades envolvidas pertencem ao mesmo tenant.
- Recursos de outro tenant devem ser tratados sem revelar sua existência, conforme política de erros da API.
- Operações sem tenant são permitidas apenas em escopos de plataforma explicitamente modelados e autorizados.

Filtros globais do EF Core podem formar uma camada de defesa, mas não substituem autorização, validação de comandos e testes. Qualquer bypass administrativo deve ser explícito, restrito e auditável.

## Integridade e banco

Entidades tenant-scoped terão `TenantId` obrigatório. Unicidade normalmente será composta por tenant (por exemplo, `TenantId + chave de negócio`). Relacionamentos que possam cruzar tenants exigem validação de domínio/aplicação e, quando viável, restrições compostas no banco. Índices devem começar por `TenantId` quando alinhados às consultas reais.

## Platform scope e tenant scope

Platform Admin não é um Tenant Admin “mais forte”. Endpoints e políticas de plataforma são separados, operam sem simular implicitamente um tenant e geram auditoria nas ações críticas. O acesso de suporte a dados de tenant, se futuramente necessário, exigirá decisão específica, justificativa, menor privilégio e trilha de auditoria.

## Testes obrigatórios

Para cada nova entidade tenant-scoped, testes automatizados cobrirão ao menos, nas duas direções:

- A não lê B;
- A não altera B;
- A não exclui B;
- tentativa de IDOR com ID válido do outro tenant;
- criação e associação não aceitam `TenantId` forjado;
- vínculo entre entidades de tenants diferentes é rejeitado.

Esses testes serão implementados nas fases que criarem as entidades; a F0 não cria código nem banco.
