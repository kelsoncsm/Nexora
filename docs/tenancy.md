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

Um usuário autenticado pode listar as próprias empresas ativas em `GET /api/v1/me/tenants` (id, nome, slug, papel) para reentrar em um tenant sem digitar o slug. O endpoint só devolve vínculos ativos de tenants ativos e nunca os de outro usuário. A seleção efetiva continua sendo feita por `POST /api/v1/t/{tenantSlug}/session`, que revalida a associação e reemite a sessão; o slug permanece apenas chave de resolução.

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

## Gestão de membros e convites

O acesso de uma pessoa a uma empresa é modelado por `TenantUser` (vínculo ativo + `TenantRoleId`). A gestão desse acesso tem **granularidade CRUD própria**, separada do gate grosso `tenant.manage`:

| Permissão | Cobre |
|---|---|
| `tenant.members.read` | listar membros e convites; ver a lista de papéis atribuíveis |
| `tenant.members.create` | enviar e reenviar convite |
| `tenant.members.update` | trocar o papel de um membro |
| `tenant.members.delete` | desativar um membro; cancelar um convite |

A migration `20260902150000_AddTenantMemberPermissions` faz backfill das quatro chaves em todo papel que já tinha `tenant.manage` (todo ADMIN de sistema e qualquer papel custom de administração), então nenhuma empresa perde a capacidade atual. `tenant.manage` **não é removido** — ele ainda cobre perfil da empresa, papéis e billing (split desse resíduo é um gap arquitetural aberto, `docs/NEXORA-UI-COVERAGE.md §7`).

### Fluxo de convite

Endpoints sob `/api/v1/tenant/members/invitations` (+ `/accept` anônimo). O tenant é **sempre** o da sessão do chamador — nunca vem do corpo. Um `TenantInvitation` guarda só o **hash SHA-256** do token; o token bruto vive apenas no e-mail de convite (payload do outbox, redigido para `{}` assim que a mensagem é enviada) e é **de uso único**: aceitar, cancelar ou expirar torna o token inerte. "Expirado" é derivado de `ExpiresAt` (config `Tenancy:Invitations:ExpirationDays`, padrão 7), sem job de estado. Índice único parcial `(TenantId, NormalizedEmail) WHERE Status='Pending'` — no máximo um convite pendente por (empresa, e-mail).

- **Criar** valida e-mail, exige que o e-mail não seja membro ativo, e aplica a regra de não escalonamento (abaixo). Convite pendente e ainda válido → 409; pendente mas expirado → reemitido na mesma linha.
- **Reenviar** rotaciona o token (o anterior deixa de valer) e recompõe a janela de expiração; só em convite `Pending`.
- **Cancelar** marca `Cancelled`; só em convite `Pending`.
- **Aceitar** (anônimo, rate-limited em `auth`): o token é a única credencial e viaja no corpo. E-mail novo → cria a identidade pelo mesmo caminho do registro (política de senha, hasher, normalização); e-mail já existente → exige a senha da conta (`VerifyCredentialsAsync`). O `TenantId`/`TenantRoleId` do vínculo vêm **do convite**, nunca do corpo. Cria (ou reativa) exatamente um `TenantUser`; o convite vira `Accepted`.

### Não escalonamento de privilégio

`RoleGrant.IsWithinActorAuthority` — regra **`PermissõesResultantesDoPapel ⊆ PermissõesEfetivasDoAtor`**, sem exceção para `tenant.manage` nem para papéis de sistema. Aplicada em quatro pontos:

- **atribuir papel a membro** — `AssignRoleAsync` → 403 `TenantForbiddenException`;
- **criar / reenviar convite** — `EnsureActorCanGrantRoleAsync`;
- **editar permissões de papel custom** — `SetRolePermissionsAsync` rejeita (antes de qualquer escrita) qualquer conjunto que não seja subconjunto das permissões do ator, fechando o "editar um papel existente para conceder a si mesmo — ou a quem já está nele — uma permissão que você não tem";
- **lista de papéis atribuíveis** — `GetAssignableRolesAsync` filtra o que o front oferece.

Segurar `tenant.members.create` **não** permite convidar alguém para o papel ADMIN do sistema, e segurar `tenant.manage` **não** permite conceder permissões arbitrárias a um papel — a menos que o ator detenha o conjunto completo de permissões. (Papel de sistema continua imutável via `SetRolePermissionsAsync`; criar um papel custom já nasce inerte se tiver permissões acima do ator, pois não pode ser atribuído nem usado em convite.)

### Auditoria

Eventos `tenant_member.invited`, `tenant_invitation.{resent,cancelled,accepted}` — cada um com `TenantId` e `ActorUserId` (no aceite, o ator é o próprio convidado). `Details` nunca contém token bruto, hash do token, senha, hash de senha nem o link de aceite completo.

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
