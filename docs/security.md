# Segurança

## Princípios

Segurança é responsabilidade do backend e usa defesa em profundidade, menor privilégio e negação por padrão. O Angular pode ocultar controles por UX, mas o servidor autentica, resolve o tenant, autoriza e valida toda operação.

## Autenticação

A estratégia inicial é credencial com senha protegida por algoritmo de hashing adequado ao framework, access token JWT de curta duração e refresh token revogável. A implementação deverá:

- usar o password hasher seguro e suportado pela versão adotada do ASP.NET Core, com parâmetros atualizáveis;
- validar assinatura, expiração, issuer e audience do JWT;
- gerar refresh tokens com entropia criptográfica e armazenar somente representação protegida/hash;
- rotacionar refresh tokens no uso, revogar a cadeia quando houver reutilização suspeita e permitir logout/revogação;
- não transportar tokens em URL nem registrá-los;
- manter chaves e secrets fora do repositório e permitir rotação.

Tempos exatos de expiração e o transporte/armazenamento do refresh token no cliente serão definidos na F2 com análise de ameaça e configuração por ambiente. Eles não são inventados na F0.

### Estratégia aprovada na F2

- JWT de acesso: 15 minutos por padrão, configurável, mantido somente em memória na SPA.
- Refresh token: 7 dias por padrão, configurável, aleatório e armazenado no cliente somente em cookie `HttpOnly`, `Secure` fora de Development, `SameSite=Strict`.
- Persistência: somente hash SHA-256 do refresh token, nunca o valor bruto.
- Rotação: cada refresh revoga o token anterior e emite outro na mesma família; reutilização revoga a família ativa.
- Logout: revoga o refresh token apresentado e remove o cookie.
- Chave JWT: secret externo obrigatório com pelo menos 32 caracteres; issuer, audience, assinatura e expiração são validados.

Detalhes e alternativas estão no `ADR-0007-f2-token-session-strategy.md`.

## Autorização

Autorização será server-side e baseada em políticas/permissões explícitas, como `customers.read`, em vez de condicionais de role espalhadas. Roles agregam permissions; plano/feature não substitui permissão. A decisão efetiva considera identidade, escopo (plataforma ou tenant), associação ao tenant, permissão e feature aplicável.

Platform Admin e Tenant Admin usam políticas e superfícies de API distintas. Ações administrativas críticas são auditáveis.

Na F4, endpoints `/api/v1/admin/...` exigem a policy `PlatformAdmin`, baseada em `platform.access`, e rejeitam tokens tenant-scoped com `tenant_id`. O bootstrap opcional promove apenas um usuário existente indicado por configuração operacional; não há endpoint público de promoção.

Na F6, autorização operacional usa `TenantRole` e capabilities globais estáveis. O middleware revalida vínculo e permissões no tenant indicado pela claim emitida pelo servidor, removendo permissions globais da identidade tenant-scoped antes de aplicar policies.

A feature aplicável é aplicada em runtime (ADR-0019): grupos de rotas de módulo passam por um endpoint filter `RequireFeature` que resolve `IFeatureAccessService.ResolveAsync` e rejeita com `403 feature_not_in_plan` quando o plano/override não inclui o módulo; limites de plano são verificados na criação (`409 plan_limit_reached`). Feature não substitui permissão — as duas camadas são independentes.

## Controles mínimos

- isolamento tenant e proteção contra IDOR;
- validação de entrada, tamanho de requests e valores derivados;
- HTTPS em produção e CORS restritivo;
- headers de segurança e rate limiting proporcionais ao endpoint;
- tratamento global de erros, sem detalhes internos em produção;
- proteção de endpoints administrativos e webhooks;
- migrations versionadas e plano de backup/restore;
- dependências e configurações revisadas por fase.

## Dados e logs

Nunca registrar senha, hash de senha sem necessidade, access token, refresh token bruto, API key, secret ou dados de cartão. Logs estruturados podem conter identificadores operacionais mínimos e devem respeitar retenção e acesso apropriados. Dados sensíveis devem ser minimizados e protegidos em trânsito e repouso conforme risco e requisitos legais a definir.

## Auditoria

Eventos críticos usam registros estruturados, não apenas texto livre. Exemplos: login/revogação relevantes, alteração de permissões ou plano, desativação de tenant, ação de Platform Admin e evento financeiro importante. O evento deve permitir identificar ator, escopo, ação, alvo, instante, resultado e correlação sem guardar segredo.

`AuditLog` (`platform.audit_logs`, append-only) grava, além das ações de plataforma já cobertas, as ações sensíveis de administração de tenant e de plano/billing (ADR-0021, P2.9): `member.role_changed`, `member.deactivated`, `role.permissions_changed`, `tenant_feature_override.configured`, `plan_feature.configured` e `billing.checkout_requested`. Cada linha tem `ActorUserId` (humano, do contexto autenticado — nunca do body), `TenantId` (nulo para ação de catálogo global), `CorrelationId` e um `Details` JSON estruturado com ids e códigos — nunca token, URL de checkout, payload do gateway, senha ou PII desnecessária. A linha de auditoria é gravada na mesma transação da mutação: operação revertida não deixa trilha; mudança sem diff não gera evento; retry idempotente de checkout/webhook não duplica. Transições de subscription feitas por sistema ficam em `SubscriptionEvent`. Retenção ainda não definida.

## Verificação por fase

Cada fase revisará autenticação/autorização aplicável, tenant isolation, IDOR, validação, exposição em logs e testes negativos. A F14 consolida hardening, rate limits, backup/restore, observabilidade e revisão de produção; isso não adia controles essenciais das fases anteriores.

Na configuração `Production`, a API falha no startup sem secrets/providers reais, hosts e origens HTTPS explícitos e sem pelo menos um proxy reverso confiável (`ReverseProxy:KnownProxies` ou `ReverseProxy:KnownNetworks`) válido. HSTS, headers de segurança, proxy confiável e rate limits segmentados são aplicados no pipeline HTTP. Segredos produtivos pertencem ao Key Vault e não ao Git, bundle Angular, imagem ou YAML.
