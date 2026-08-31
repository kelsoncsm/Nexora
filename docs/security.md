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

## Autorização

Autorização será server-side e baseada em políticas/permissões explícitas, como `customers.read`, em vez de condicionais de role espalhadas. Roles agregam permissions; plano/feature não substitui permissão. A decisão efetiva considera identidade, escopo (plataforma ou tenant), associação ao tenant, permissão e feature aplicável.

Platform Admin e Tenant Admin usam políticas e superfícies de API distintas. Ações administrativas críticas são auditáveis.

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

## Verificação por fase

Cada fase revisará autenticação/autorização aplicável, tenant isolation, IDOR, validação, exposição em logs e testes negativos. A F14 consolida hardening, rate limits, backup/restore, observabilidade e revisão de produção; isso não adia controles essenciais das fases anteriores.
