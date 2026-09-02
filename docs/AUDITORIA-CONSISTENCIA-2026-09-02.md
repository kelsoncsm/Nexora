# AUDITORIA TÉCNICA NEXORA — 2026-09-02

> Auditoria **somente leitura**. Nenhum arquivo de código alterado, nenhuma migration, nenhum commit,
> nenhuma alteração de banco (exceto criação do database efêmero `nexora_audit_tests` para rodar os
> testes de integração — pode ser removido com `DROP DATABASE nexora_audit_tests`).
> Fonte da verdade: código atual + migrations + testes + build + docs. Divergências doc↔código estão marcadas.

---

## 1. STATUS GERAL

**ATENÇÃO** (não CRÍTICO, não SAUDÁVEL)

Justificativa: nenhuma falha P0 confirmada — o isolamento multi-tenant é consistente (filtro manual em todos
os serviços + FKs compostas `HasPrincipalKey(TenantId,Id)` que tornam referência cross-tenant estruturalmente
impossível + testes de isolamento passando nas duas direções); o "gate" de RBAC suspeito **foi corrigido** e
está travado por teste; segredos limpos; autenticação sólida; build e 79 testes verdes.

Porém há **1 lacuna arquitetural relevante** (planos/features resolvem mas **não são aplicados** em lugar
nenhum — a promessa central do SaaS "plano controla módulos" não existe em runtime), **defaults de produção
inseguros** para proxy reverso, **1 permissão trocada** em operação de equipe e **UX quebrada de reentrada
de tenant** para usuário que retorna.

---

## 2. RESUMO EXECUTIVO

1. Arquitetura: Modular Monolith em camadas (Api/Application/Domain/Infrastructure com subpastas por feature) — Clean Architecture respeitada, `DependencyDirectionTests` garante a direção.
2. Multi-tenancy **seguro**: todo serviço tenant-scoped filtra por `TenantId`; FKs compostas impedem cross-tenant; `TenancyIsolationTests`/`CatalogIsolationTests`/`CustomerAuthorizationTests` passam em PostgreSQL real.
3. **RBAC gate CORRIGIDO** (era a suspeita da Seção 6): `IdentityService.BuildSession` agora embute as permissões de tenant no JWT quando um tenant é selecionado; `TenantContextMiddleware` troca claims por requisição antes das policies e preserva `identity.profile`. `AuthRbacTokenTests` (3 testes) travam isso.
4. `identity.profile` sobrevive à sessão de tenant → `GET /identity/me` funciona dentro do tenant (testado). Sem workaround escondendo problema.
5. Platform Scope × Tenant Scope: policy `PlatformAdmin` exige `platform.access` **e ausência de** `tenant_id`; endpoints `/api/v1/admin/**` num grupo único protegido. `PlatformAdministrationTests` passa.
6. Autenticação: JWT 15min validado (issuer/audience/assinatura/lifetime), refresh 7d rotativo com detecção de reuso (CAS atômico no PG), cookie `HttpOnly`/`Secure`/`SameSite=Strict` escopo `/api/v1`, só hash SHA-256 persistido, senha 12–128.
7. **Planos/Features: catálogo + resolver + provider persistente existem e funcionam — mas NINGUÉM chama `IFeatureAccessService.ResolveAsync`.** Nenhum limite (max profissionais/clientes) é verificado. F5 não atende o critério em runtime. **P1.**
8. **Sem endpoint "minhas empresas".** `LoginAsync` sempre cria sessão sem tenant. Usuário que retorna precisa **digitar o slug** para reentrar. `/clientes` e todas as rotas tenant redirecionam para `/` logo após login. **P1.**
9. **Defaults de produção inseguros**: `ReverseProxy:KnownProxies/KnownNetworks` vazios e não validados → atrás de proxy o rate limiter de `auth` (10/min por IP) vira global e `UseHttpsRedirection` pode entrar em loop. **P1.**
10. `TenancyService.DeactivateMembershipAsync` gateia em **`customers.delete`** para desativar membro da equipe (deveria ser `tenant.manage`). **P1.**
11. Billing (`GET /subscription`, `GET /billing/invoices`, `POST /billing/checkout`) gateado só por `tenant_id` — qualquer membro cria checkout. **P2.**
12. `GET /api/v1/tenant/members` sem permissão — qualquer membro lista e-mails de todos. **P2.**
13. Frontend sem interceptor de erro/refresh: token expira em 15min → próxima chamada falha até reload. **P2.**
14. Auditoria (`AuditLog`) cobre só tenant ativa/desativa e segmento. **Não cobre** mudança de papel/permissões, desativação de membro, ações de subscription (só `SubscriptionEvent`), login/logout. **P2.**
15. Migrations/banco: índices compostos `(TenantId, …)` em todas as entidades tenant-scoped; unique constraints de negócio; idempotência de webhook/pagamento por unique index. Snapshot e migrations consistentes com as entidades. Sem xmin (usa advisory lock + serializable + CAS).
16. API: Problem Details via `GlobalExceptionHandler`, sem vazar detalhe interno em produção; status codes conforme `api-guidelines`. Listagens sem paginação exceto `/customers`. **P2/P3.**
17. Segurança de segredos: **zero segredos no repositório**; placeholders vazios; `ProductionConfiguration` derruba o startup sem segredos/hosts/origens HTTPS reais.
18. Webhook MP: HMAC-SHA256 + tolerância de timestamp + `FixedTimeEquals`; `AllowAnonymous` + rate-limit; tabela de idempotência.
19. Dependências: `npm audit` **0 vulnerabilidades**; backend central package management, tudo alinhado em .NET 10 / EF 10 / Npgsql 10; `dotnet list --vulnerable` limpo.
20. Build backend **0 warning / 0 erro**; frontend verde (budget estourado ~219 kB = dívida); **testes: 14 unit + 50 integração (PostgreSQL real) + 15 frontend = 79, todos verdes, 0 ignorados**.

---

## 3. INCONSISTÊNCIAS CRÍTICAS — P0

**Nenhuma confirmada.**

Analisadas e descartadas como P0:
- Ausência de EF global query filter → **não** é P0 porque todo serviço filtra manualmente por `TenantId` E as FKs compostas (`HasPrincipalKey(new{TenantId,Id})` em appointments, working_hours, blocked_periods, professional_services) tornam uma referência cross-tenant estruturalmente rejeitada pelo banco. Testes de isolamento nas duas direções passam. Rebaixado para **P3** (defesa-em-profundidade ausente).
- `TenantId` do cliente como autoridade → **não ocorre**: todo endpoint tenant-scoped lê `ITenantContext.TenantId`, populado pelo `TenantContextMiddleware` a partir da claim `tenant_id` emitida pelo servidor após `ValidateMembershipAsync`. `CreateAsync` recebe o tenant do contexto, nunca do DTO.
- Vazamento de segredo → **não há** segredo no repo.

---

## 4. INCONSISTÊNCIAS IMPORTANTES — P1

### P1.1 — Planos/Features resolvem mas NÃO são aplicados (enforcement inexistente)
> **RESOLVIDO em 2026-09-02 (ADR-0019, sem commit).** Endpoint filter `RequireFeature` nos cinco grupos de módulo, limites em `CustomerService.CreateAsync`/`CatalogService.CreateProfessionalAsync` (409 `plan_limit_reached`), features efetivas no `GET /identity/me`, `featureGuard` + navGroups no frontend, migration data-only `20260902000000_SeedFeatureCatalog`. Tenant sem `Subscription` → 403 `feature_not_in_plan` em todos os módulos, por design.
- **Problema:** `IFeatureAccessService.ResolveAsync(tenantId, featureCode)` implementa a cadeia override → plan → default corretamente, e `PersistentTenantPlanProvider.GetCurrentPlanIdAsync` deriva o plano da `Subscription` vigente. Porém **nenhum endpoint, serviço, middleware ou guard chama `ResolveAsync`**. Nenhum limite (`FeatureAccess.Limit`, ex. máx. profissionais/clientes) é verificado em `CreateProfessionalAsync`/`CreateAsync`.
- **Evidência:** `grep -rn "IFeatureAccessService\|IsFeatureEnabled\|RequireFeature\|HasFeature" backend/src` → **apenas** a linha de registro DI (`DependencyInjection.cs:62`). `FeatureAccessTests` testa só o resolver isolado com um fake provider; não existe teste "plano Básico bloqueia /reports".
- **Arquivos:** `Nexora.Infrastructure/Plans/FeatureAccessService.cs` (não consumido), `Nexora.Api/**Endpoints.cs` (nenhum verifica feature), `CustomerService.CreateAsync`/`CatalogService.CreateProfessionalAsync` (sem checagem de limite). Frontend: `app.ts` `navGroups` gateia só permissão, nunca feature.
- **Impacto:** um tenant no plano "Básico" acessa Relatórios, cria profissionais/clientes ilimitados; desativar um plano/feature no Platform Admin **não tem efeito nenhum** no acesso. `security.md` ("a decisão efetiva considera identidade, escopo, associação ao tenant, permissão **E feature aplicável**") e `api-guidelines.md` ("Toda operação define requisitos de autenticação, permissão, escopo **e feature**") não são cumpridos. Critério da F5 ("O acesso aos módulos é controlado por plano/configuração") **NÃO atendido em runtime**.
- **Recomendação:** criar um `IAuthorizationHandler`/filtro `RequireFeature("REPORTS")` ou um `IFeatureGate` chamado nos endpoints; verificar `Limit` em `Create*`; expor as features efetivas no `/identity/me` ou num `GET /api/v1/features` tenant-scoped para o frontend gatear a sidebar.

### P1.2 — Usuário que retorna não reentra na empresa (sem tenant discovery)
- **Problema:** `IdentityService.LoginAsync` → `CreateSessionAsync(user, ..., null)` — **login sempre gera sessão SEM tenant**. Não existe endpoint que liste as empresas do usuário (`POST /api/v1/tenants` só cria; não há `GET /api/v1/tenants` nem `GET /api/v1/me/tenants`). O `home-page.ts` pede o **slug digitado** (`select(slug.value)`).
- **Evidência:** `grep MapGet.*tenants` → só `/t/{tenantSlug}` (resolve público) e `/tenant/members` (já precisa de contexto). `home-page.html` tem `<input #slug placeholder="minha-empresa">`. `auth-page.ts:36` navega para `/` após login sem `selectTenant`.
- **Arquivos:** `IdentityService.LoginAsync`, `TenancyEndpoints.cs` (ausência de rota), `home-page.ts`/`home-page.html`, `tenant.guard.ts`.
- **Impacto:** depois de `logout` (cookie apagado) + novo login, o usuário de 1 empresa cai numa tela pedindo o slug exato que ele não decora. `/clientes`, `/agenda` etc. redirecionam para `/` (ver P2.5). Onboarding funciona (chama `selectTenant`), mas o retorno não.
- **Recomendação:** `GET /api/v1/me/tenants` (id, name, slug, role) usando o índice `(UserId, IsActive)` já existente; auto-`selectTenant` quando houver exatamente 1; seletor de empresa na UI quando houver mais.

### P1.3 — Defaults de produção inseguros para proxy reverso
- **Problema:** `appsettings*.json` trazem `ReverseProxy:KnownProxies: []` e `KnownNetworks: []`, e `ProductionConfiguration.ValidateProductionConfiguration` **não valida** esses campos. Atrás de nginx/Azure Front Door sem configurar: `UseForwardedHeaders` ignora `X-Forwarded-For`/`X-Forwarded-Proto`.
- **Evidência:** `ServiceCollectionExtensions.AddApiServices` monta `KnownProxies` a partir de `configuration.GetSection("ReverseProxy:KnownProxies")`; o rate limiter particiona por `context.Connection.RemoteIpAddress`. `ProductionConfiguration` valida ConnectionString/SigningKey/MP/Resend/AppInsights/Cors/AllowedHosts — não `ReverseProxy`.
- **Impacto:** (a) rate limiter `auth` (10/min) vira **global** (IP do proxy) → 10 tentativas de login de qualquer pessoa bloqueiam login da plataforma inteira; (b) `UseHttpsRedirection` vê `http` → 307 → possível loop com o proxy; (c) IP de auditoria/log incorreto.
- **Recomendação:** validar `ReverseProxy` no `ProductionConfiguration` (exigir ao menos um `KnownProxies` ou `KnownNetworks` quando `Production`), documentar o valor por ambiente no runbook de deploy.

### P1.4 — Desativação de membro da equipe gateada na permissão errada
- **Problema:** `TenancyService.DeactivateMembershipAsync` verifica `x.TenantRole.Permissions.Any(p=>p.PermissionKey==TenantPermissions.CustomersDelete)` para autorizar a desativação de um **membro da equipe**.
- **Evidência:** `Nexora.Infrastructure/Tenancy/TenancyService.cs` (método `DeactivateMembershipAsync`, linha ~40). O endpoint `PATCH /api/v1/tenant/members/{id}/deactivate` está só sob `.RequireAuthorization()` (sem policy de permissão), delegando a decisão ao serviço.
- **Impacto:** um papel de "recepção" com `customers.delete` (para excluir clientes) consegue **desativar colegas**. Escopo limitado (mesmo tenant, não escala privilégio próprio), mas é um controle de acesso incorreto.
- **Recomendação:** trocar por `TenantPermissions.TenantManage` (e idealmente mover o gate para o endpoint, como no grupo `/api/v1/tenant` admin).

---

## 5. INCONSISTÊNCIAS MÉDIAS — P2

- **P2.1 — Billing sem gate de permissão.** `GET /api/v1/subscription`, `GET /api/v1/billing/invoices`, `POST /api/v1/billing/checkout` só exigem `RequireClaim("tenant_id")`. Qualquer membro vê a assinatura e **dispara um checkout**. Deveria exigir `tenant.manage`. Frontend: menu "Plano e Assinatura" e `/assinatura` sem gate de permissão. `TenantBillingEndpoints.cs`.
- **P2.2 — `GET /api/v1/tenant/members` (e `/t/{slug}/members`) sem permissão.** Só `.RequireAuthorization()`. Qualquer membro autenticado lista **e-mail + papel + status de todos os membros**. `TenancyEndpoints.cs`.
- **P2.3 — Frontend sem interceptor de erro/refresh.** Só `auth.interceptor` (anexa Bearer). Não há 401 → `refresh()` → retry; quando o access token de 15min expira, a próxima chamada falha com erro genérico da tela até o usuário recarregar. Sem toast/handler global de erro. `api-guidelines.md` pede padronização de erros. `frontend/nexora-web/src/app/core/**`.
- **P2.4 — Sem `GET /me/tenants`** (raiz de P1.2) — impede um seletor de empresa.
- **P2.5 — Rotas tenant redirecionam para `/` logo após login.** `tenantGuard` → `auth.tenantId() ? true : '/'`. Determinístico após login (token sem `tenant_id`), parece "intermitente" só porque o cookie tenant-scoped às vezes sobrevive de uma sessão anterior. Consequência de P1.2.
- **P2.6 — Listagens sem paginação** exceto `/customers` (que tem page/pageSize/sort/search + total). `/professionals`, `/services`, `/appointments` (por janela de data), `/admin/users` retornam a coleção inteira. `api-guidelines.md` pede paginação para listas potencialmente grandes. `NxPagination` (frontend) foi criado mas não está plugado.
- **P2.7 — Campos de UUID crus na UI.** `schedule-page` (customerId/professionalId/serviceId no modal de agendamento; professionalId nos modais de disponibilidade/bloqueio) e telas de Admin (tenantId/planId em Assinaturas e Preços). Parte por limitação de endpoint (Admin não tem "list tenants" para o select), parte por não ter sido feito (scheduling tem `/professionals` e `/customers` disponíveis).
- **P2.8 — `subscription-admin-page.changePlan()` usa `window.prompt`** — dialog bloqueante do browser; deveria ser um modal (`NxModal`).
- **P2.9 — Auditoria incompleta.** `AuditLog` (append-only, com ator/ação/alvo/resultado/correlação/instante) é gravado só em `SetTenantActiveAsync`, `CreateSegmentAsync`, `UpdateSegmentAsync` (`AdministrationService`). **Não** em: `CreateRoleAsync`/`UpdateRoleAsync`/`SetRolePermissionsAsync`/`AssignRoleAsync` (mudança de papéis/permissões — `security.md` lista como evento crítico), `DeactivateMembershipAsync`, ações de subscription (guardam `SubscriptionEvent`, mas não `AuditLog`; correlação só via `context.TraceIdentifier`), login/refresh/logout/falha de login.

---

## 6. DÍVIDA TÉCNICA — P3

- **P3.1** — Sem EF global query filter (`HasQueryFilter`). Aceitável pela `tenancy.md`, mas remove a camada de defesa: um `.Where(TenantId==...)` esquecido num serviço novo = vazamento silencioso. Recomenda-se filtro global + `IgnoreQueryFilters` explícito nos serviços de plataforma.
- **P3.2** — `dotnet format` não limpo: 6 arquivos pré-existentes (`Program.cs`, `ServiceCollectionExtensions.cs`, `GlobalExceptionHandler.cs`, `IdentityEndpoints.cs`, `TenancyEndpoints.cs`, `TenantContextMiddleware.cs`) com ordenação de `using`/whitespace fora do `.editorconfig`.
- **P3.3** — Helper `UserId(ClaimsPrincipal)` duplicado em ~7 arquivos de endpoint. Extrair para `ClaimsPrincipalExtensions`.
- **P3.4** — `TenancyEndpoints.cs` mistura tenancy pública (resolve slug, create, session, members) e administração do tenant (grupo `/api/v1/tenant`) num arquivo; 3 rotas sem `.WithTags` (tag = nome da classe no Swagger).
- **P3.5** — Bundle inicial do Angular ~219 kB acima do budget (dívida da migração DentalFlow; `package.json` intacto).
- **P3.6** — `NxPagination` implementado, nenhuma lista o usa (backend não pagina — ver P2.6).
- **P3.7** — Webhook: `eventId = input.Id?.ToString() ?? $"{input.Action}:{dataId}"` — o fallback pode colidir se o Mercado Pago não enviar `Id` numérico.
- **P3.8** — `IdentityService` mistura estilos (métodos formatados vs. comprimidos numa linha `RefreshAtomicallyAsync`); idem em vários serviços de Infrastructure (`CustomerService`, `CatalogService`, `SchedulingService` uma-linha).
- **P3.9** — Estrutura de pastas diverge do MASTER_PLAN (§5 previa `backend/src/Modules/<Feature>/`; implementado como camadas com subpastas por feature). Permitido pela F1 ("pode ser adaptada... desde que a decisão seja documentada"), mas **nenhum ADR** registrou a decisão.
- **P3.10** — `notes.md` e `db-set-password.ps1` na raiz, não rastreados (arquivos de sessões anteriores). `db-up.bat`/`db-down.bat` rastreados. Não é bug, é sujeira.

---

## 7. AUTH / RBAC

| Item | Estado |
|---|---|
| JWT | HS256, `sub`/`email`/`jti`/`permission`*/`role`*/`tenant_id`?; 15min; issuer+audience+assinatura+lifetime validados; ClockSkew 30s. |
| Global Permissions | Só `identity.profile` (`GlobalPermissions.All`). Viajam sempre no token. |
| Tenant Permissions | `customers.* / professionals.* / services.* / appointments.* / reports.read / tenant.manage`. **Embutidas no JWT quando um tenant é selecionado** (`BuildSession` chama `tenancy.GetPermissionsAsync`). |
| BuildSession | `permissions = global; if (tenantId) permissions += tenant`. `AccessTokenGenerator` emite claim `permission` para cada uma + `tenant_id`. |
| TenantContextMiddleware | Roda entre `UseAuthentication` e `UseAuthorization`. Valida `ValidateMembershipAsync`; se rota tem `tenantSlug`, confere `routeTenant.Id == tenantId` (senão 404); **remove** claims `permission` que não estão em `GlobalPermissions.All` e **injeta** as de `tenancy.GetPermissionsAsync(tenantId,userId)`. Preserva `identity.profile`. |
| Policies | `PlatformAdmin` = `platform.access` **+ sem** `tenant_id`. Uma policy por permissão de tenant = claim `permission` **+** `tenant_id`. |
| Frontend permissions | `auth.permissions()` lê o array `permission` do JWT. `has('x')` na sidebar → agora **funciona** porque as perms de tenant estão no token. |
| Sidebar | `app.ts navGroups` gateia por permissão (`has('customers.read')` etc.). **Não** gateia por feature. |
| identity.profile | Sobrevive ao swap. `GET /identity/me` (policy `RequireClaim("permission","identity.profile")`) **funciona dentro do tenant** (testado por `AuthRbacTokenTests`). `GetUserAsync` devolve só as permissões globais — o `profile-page.ts` usa `auth.permissions()` (JWT) quando há tenant, sem workaround. |

**Conclusão:** **CORRIGIDO.** O gate suspeito (JWT sem permissões de tenant → sidebar escondida) não existe mais.
`AuthRbacTokenTests` (`PlainLoginTokenCarriesOnlyGlobalIdentityPermissions`, `TenantSessionTokenCarriesTenantPermissionsAndKeepsIdentityProfile`, `ReducedRoleTokenOnlyCarriesTheGrantsItWasGiven`) travam o comportamento e passam.
Ressalva menor: mudança de papel/permissão só reflete no JWT no próximo `refresh`/`selectTenant` (até 15min); a API em si é imediata (middleware revalida por requisição).

---

## 8. MULTI-TENANCY

| Item | Estado |
|---|---|
| Origem do TenantId | Claim `tenant_id` emitida pelo servidor após `SelectTenantAsync` valida `TenantUser` ativo + `Tenant` ativo. Refresh token carrega `TenantId` → refresh preserva o escopo. |
| Resolução | `TenantContextMiddleware` → `initializer.Initialize(tenantId, userId)` → `TenantContext` (imutável, `throw` se acessado sem init). |
| Query filters | **Nenhum `HasQueryFilter`/`IgnoreQueryFilters` em todo o backend.** Isolamento 100% manual. |
| SaveChanges | **Nenhum override / interceptor.** `Create*` recebe `tenantId` do contexto e passa para o construtor da entidade. |
| Serviços tenant-scoped | `CustomerService`, `CatalogService`, `SchedulingService`, `ReportService`, `TenancyService.Administration`, `SubscriptionService` (via tenant), `Onboarding` — **todos** filtram `.Where(x=>x.TenantId==t)` em toda query, inclusive `SingleOrDefault` por id. Validação cross-entidade (`SchedulingService.BuildAsync`, `CatalogService.LinkAsync`) confere que **todas** as entidades pertencem ao mesmo tenant. |
| FKs compostas | `appointments`, `working_hours`, `blocked_periods`, `professional_services` usam `HasForeignKey(new{TenantId,X})` + `HasPrincipalKey(new{TenantId,Id})` → **referência cross-tenant é rejeitada pelo banco**. |
| Endpoints | Lêem `ITenantContext.TenantId` ou usam policy `RequireClaim("tenant_id")`; nunca aceitam `TenantId` de body/query/rota como autoridade. |
| Admin | `AdministrationService` roda queries sem filtro (correto — escopo plataforma); endpoints sob `PlatformAdmin` (exige ausência de `tenant_id`). |
| Riscos | (a) sem global filter → disciplina futura é a única barreira em código; (b) `GetTenantReportAsync`/`ReportService` lê `ITenantContext` diretamente e joga se não disponível — mitigado pela policy `reports.read` que exige `tenant_id`. |

**Conclusão:** **SEGURO** no estado atual (isolamento consistente + FKs compostas + `TenancyIsolationTests`/`CatalogIsolationTests`/`CustomerAuthorizationTests`/`TenantAdministrationPostgresTests` passando em PG real, cobrindo A-não-lê-B, A-não-altera-B, IDOR com id válido do outro tenant, criação não aceita `TenantId` forjado). **Ressalva P3.1:** sem defesa-em-profundidade via global filter.

---

## 9. PLANS / FEATURES

| Item | Estado |
|---|---|
| Backend (entidades) | `Feature`, `Plan`, `PlanFeature`, `TenantFeatureOverride`, `PlanPrice`, `Subscription`, `SubscriptionEvent` — todos existem, configurados, com migrations. |
| Banco | `features`/`plans` unique por `Code`; `plan_features` PK composta; `tenant_feature_overrides` unique `(TenantId, FeatureId)`; `plan_prices` unique `(PlanId, BillingInterval, Currency)` filtrado por `IsActive`; `subscriptions` unique `(TenantId)` filtrado por status vivo. |
| FeatureResolver | `FeatureAccessService.ResolveAsync`: feature inativa → `(false,null)`; override do tenant vence; senão plano vigente via `ITenantPlanProvider`; senão `PlanFeature`; senão `(false,null)`. **Lógica correta.** `FeatureAccessTests` cobre plano/override/missing. |
| Subscription | `PersistentTenantPlanProvider.GetCurrentPlanIdAsync` deriva o `PlanId` da `Subscription` (Trialing/Active/PastDue) que concede entitlement (avalia lifecycle + grace). **F9 é a fonte Tenant→Plan.** |
| Frontend | Sidebar (`app.ts`) gateia só por permissão. Onboarding lista planos públicos. Nenhuma tela consome features. |
| Routes | Nenhuma rota Angular tem feature gate. |
| **Server-side enforcement** | **INEXISTENTE.** `grep IFeatureAccessService` = só o registro DI. Nenhum endpoint chama `ResolveAsync`. Nenhum `Create*` verifica `Limit`. |

**Conclusão:** **NÃO IMPLEMENTADO (enforcement).** O catálogo, o resolver e o provider persistente estão prontos e testados isoladamente, mas **nada os consome** — planos e limites não restringem nenhum módulo nem nenhuma criação. É a maior lacuna funcional do projeto.

---

## 10. MENUS / ROTAS / TELAS / BACKEND

| Menu (sidebar) | Rota Angular | Componente | Guard | Backend / Endpoint | Permission (backend) | Feature | Status |
|---|---|---|---|---|---|---|---|
| Visão Geral | `/` | HomePage | authGuard | `GET /reports/overview` (dashboard) / `GET /subscription` | `reports.read` / `tenant_id` | — | OK |
| Agenda | `/agenda` | SchedulePage | authGuard+tenantGuard | `GET/POST/PUT/PATCH /appointments`, `/working-hours`, `/blocked-periods`, `/scheduling/context` | `appointments.*` | ❌ nenhuma | OK (UUID crus P2.7) |
| Clientes | `/clientes` | CustomerPage | authGuard+tenantGuard | `/customers` CRUD + busca + paginação | `customers.*` | ❌ | OK |
| Profissionais | `/profissionais` | CatalogPage(kind) | authGuard+tenantGuard | `/professionals` CRUD + link/unlink | `professionals.*` | ❌ | OK (sem paginação) |
| Serviços | `/servicos` | CatalogPage(kind) | authGuard+tenantGuard | `/services` CRUD | `services.*` | ❌ | OK (sem paginação) |
| Relatórios | `/relatorios` | ReportsPage | authGuard+tenantGuard | `GET /reports/overview` | `reports.read` | ❌ (deveria: feature REPORTS) | PARCIAL — feature não aplicada |
| Usuários e Equipe | `/equipe` | TeamPage | authGuard+tenantGuard | `GET /tenant/members`, `PATCH .../deactivate`, `.../role`, `GET /tenant/roles` | mistura: members sem perm; deactivate = `customers.delete` (bug); role = `tenant.manage` | — | PARCIAL — P1.4 + P2.2 |
| Plano e Assinatura | `/assinatura` | BillingPage | authGuard+tenantGuard | `GET /subscription`, `GET /billing/invoices`, `POST /billing/checkout` | só `tenant_id` (P2.1) | — | PARCIAL — sem gate de permissão |
| Configurações | `/configuracoes` | SettingsHubPage | authGuard+tenantGuard | (hub — sem backend próprio) | — | — | OK |
| — (via hub) | `/empresa` | EmpresaPage | authGuard+tenantGuard | `GET/PUT /tenant` | `tenant.manage` | — | OK |
| — (via hub) | `/perfis` | RolesPage | authGuard+tenantGuard | `GET/POST/PUT /tenant/roles` | `tenant.manage` | — | OK |
| — (via hub) | `/permissoes` | PermissionsPage | authGuard+tenantGuard | `GET /tenant/permissions`, `GET/PUT /tenant/roles/{id}/permissions` | `tenant.manage` | — | OK |
| dropdown / hub | `/perfil` | ProfilePage | authGuard | `GET /identity/me` | `identity.profile` | — | OK (read-only) |
| Administração | `/admin` | AdminPage | platformAdminGuard | `GET /admin/dashboard`, `/tenants`, `/users`, `/segments`, `/features`, `/plans`, `/audit-logs` + create/update | `PlatformAdmin` | — | OK |
| Assinaturas | `/admin/assinaturas` | SubscriptionAdminPage | platformAdminGuard | `/admin/subscriptions` + `/{id}/{action}` | `PlatformAdmin` | — | OK (UUID crus, `window.prompt` P2.8) |
| Billing | `/admin/billing` | BillingAdminPage | platformAdminGuard | `/admin/billing/prices|invoices|payments` | `PlatformAdmin` | — | OK |
| Relatórios da plataforma | `/admin/relatorios` | ReportsPage(platform) | platformAdminGuard | `GET /admin/reports/overview` | `PlatformAdmin` | — | OK |
| — (fluxo, sem menu) | `/onboarding` | OnboardingPage | authGuard | `/onboarding/drafts*`, `/segments`, `/plans`, `/complete` | auth + rate-limit | — | OK (deliberadamente sem menu) |
| — (fluxo, sem menu) | `/configuracao-inicial` | VerticalSetupPage | authGuard+tenantGuard | `GET /vertical-setup`, `POST /vertical-setup/apply` | tenant_id | — | OK (deliberadamente sem menu; chegada só via onboarding) |
| — | `/403` `/erro` `/**` | ErrorPage | — | — | — | — | OK |

**Rotas sem menu (deliberado):** `/onboarding`, `/configuracao-inicial`, `/perfil` (dropdown), `/empresa` `/perfis` `/permissoes` (via hub `/configuracoes`), `/403` `/erro` `/404`.
**Rotas órfãs de menu (não-deliberado):** nenhuma.
**Itens de menu apontando para rota inexistente:** nenhum.
**Endpoints sem tela:** `PATCH /appointments/{id}/status` e `/cancel` (usados pela agenda), `POST /admin/subscriptions/{id}/process` (não exposto na UI), `GET /admin/subscriptions/{id}/events` (não exposto), `GET /t/{slug}` público (usado no fluxo de resolução), `GET /t/{slug}/members` (duplica `/tenant/members`).
**Telas sem endpoint dedicado:** SettingsHubPage (hub puro), ErrorPage.

---

## 11. FUNCIONALIDADES AUSENTES (esperadas pela arquitetura)

| Funcionalidade | Backend | Endpoint | Frontend | Rota | Menu | Funcional? |
|---|---|---|---|---|---|---|
| Empresa — dados cadastrais | SIM | `GET/PUT /api/v1/tenant` | SIM | `/empresa` | via hub | SIM (nome + timezone; slug/titularidade não editáveis por design) |
| Usuários — listar membros | SIM | `GET /api/v1/tenant/members` | SIM | `/equipe` | SIM | SIM |
| Usuários — **convidar/criar** | **NÃO** | — | — | — | — | **NÃO** (a UI diz explicitamente "não disponível nesta versão") |
| Usuários — trocar papel | SIM | `PATCH /tenant/members/{id}/role` | SIM | `/equipe` | SIM | SIM |
| Usuários — desativar/reativar | PARCIAL | `PATCH /tenant/members/{id}/deactivate` (reativar: **não há**) | SIM (só desativar) | `/equipe` | SIM | PARCIAL (só desativa; permissão errada — P1.4) |
| Perfis/Papéis — CRUD | SIM | `GET/POST/PUT /tenant/roles` (delete: **não há**) | SIM | `/perfis` | via hub | SIM (sem exclusão de papel) |
| Permissões por papel | SIM | `GET/PUT /tenant/roles/{id}/permissions` | SIM | `/permissoes` | via hub | SIM |
| Fuso horário do tenant | SIM | `PUT /tenant` | SIM | `/empresa` | via hub | SIM |
| Preferências (notificação etc.) | **NÃO** | — | — | — | — | **NÃO** (não é escopo do MVP) |
| Perfil — editar nome/e-mail | **NÃO** | — | — | `/perfil` | dropdown | **NÃO** (read-only) |
| Perfil — **alterar senha** | **NÃO** | — | — | — | — | **NÃO** |
| Perfil — **recuperação de senha** | **NÃO** | — | — | — | — | **NÃO** |
| Perfil — troca de senha obrigatória | **NÃO** | — | — | — | — | **NÃO** (não há flag `MustChangePassword`) |
| **Listar minhas empresas / trocar de empresa** | **NÃO** | — | PARCIAL (slug digitado) | `/` | — | **NÃO** (P1.2) |
| **Feature gating (plano limita módulo)** | **NÃO** (resolver existe, não é chamado) | — | **NÃO** | — | — | **NÃO** (P1.1) |
| Limites de plano (max profissionais/clientes) | **NÃO** | — | — | — | — | **NÃO** (P1.1) |
| WhatsApp / Financeiro / CRM / Estoque | **NÃO** | — | — | — | — | **NÃO** (pós-MVP no roadmap — esperado) |
| Cupons | **NÃO** | — | — | — | — | **NÃO** (pós-MVP) |

> Ausências marcadas **NÃO** que **não** são bug: WhatsApp/Financeiro/CRM/Estoque/Cupons (pós-MVP explícito no `roadmap.md`),
> preferências. Ausências que **são lacuna real** para o MVP comercial: alterar senha, recuperação de senha,
> convite de usuário, listar/trocar empresa, feature gating.

---

## 12. BANCO / MIGRATIONS

- **20 migrations** de `AddIdentitySecurity` (F2) até `AddTenantManagePermission` (F6.1). Snapshot (`NexoraDbContextModelSnapshot`) presente. Build compila e `MigrateAsync` roda limpo contra PG novo (testes de integração).
- **Entities × Config × Snapshot:** consistentes. Todas as entidades tenant-scoped têm `TenantId` obrigatório + FK para `Tenant` (`Cascade`) + índice composto iniciando por `TenantId`.
- **Índices críticos presentes:** `customers (TenantId,Name)`+`(TenantId,Status)`; `appointments (TenantId,StartAt)`+`(TenantId,ProfessionalId,StartAt)`+`(TenantId,CustomerId)`; `professionals/services (TenantId,Name)`; `tenant_users (TenantId,UserId)` unique + `(UserId,IsActive)`; `refresh_tokens TokenHash` unique + `(UserId,FamilyId)`; `email_outbox (Status,NextAttemptAt)`.
- **Unique constraints de negócio:** `tenants.Slug`, `tenant_roles (TenantId,Name)`, `working_hours (TenantId,ProfessionalId,DayOfWeek)`, `subscriptions (TenantId)` parcial, `plan_prices (PlanId,BillingInterval,Currency)` parcial, `billing_payments (Gateway,ExternalPaymentId)`, `processed_webhook_events (Provider,ExternalEventId)`, `onboarding_drafts (UserId,Status)` parcial + `(CompletedTenantId)` parcial, `tenant_feature_overrides (TenantId,FeatureId)`.
- **Concorrência:** sem `xmin`/rowversion. Estratégias: advisory lock PG por `(tenant,professional)` (scheduling), `SERIALIZABLE` + `FOR UPDATE` (onboarding complete), CAS atômico via `ExecuteUpdateAsync` (refresh token), unique parcial (subscription). Migration `HardenPreCheckpointConcurrency` endureceu janelas de cobertura de invoice.
- **FK nullable:** `refresh_tokens.TenantId` nullable (correto — sessão sem tenant); `appointments`/`working_hours` etc. têm `TenantId` NOT NULL.
- **`OnDelete`:** entidades operacionais `Cascade` a partir de `Tenant`; FKs de negócio entre operacionais = `Restrict` (não apaga appointment ao apagar customer). `subscription_events` → `User` = `Restrict`.
- **Coluna sem migration / migration sem entity:** nenhuma detectada.
- **Índice faltante:** `blocked_periods` tem `(TenantId,ProfessionalId,StartAt)` mas a query de conflito filtra `StartAt < end && EndAt > start` — o índice cobre a parte esquerda; ok para o volume esperado. `billing_invoices` perdeu `IX_SubscriptionId` simples (trocado por unique composto) — ok.

**Conclusão:** banco **consistente e bem indexado**. Nada a corrigir.

---

## 13. API

- **Versionamento:** `/api/v1/**` em tudo. ✓
- **DTOs:** entidades EF nunca expostas — todos os endpoints retornam records de contrato (`*View`, `*Dto`). ✓
- **Status codes** (`GlobalExceptionHandler` + endpoints): 201 (create com Location), 200 (read/update), 204 (delete/no-body), 400 (validação), 401 (auth), 403 (policy), 404 (not found / cross-tenant não revela existência), 409 (conflito/unicidade), 429 (rate limit), 503 (gateway MP), 500 (inesperado, genérico + log). **Consistente com `api-guidelines.md`.** `422` (regra de negócio) não é usado — conflitos de agenda usam 409, aceitável.
- **Problem Details:** `AddProblemDetails` + `AddExceptionHandler<GlobalExceptionHandler>`; `requestId` em todo erro; `errors` dict em validação de identidade. **Sem stack trace / SQL / secret em produção.**
- **Inconsistências relevantes:**
  - **Paginação:** só `/customers`. Outras listas sem page/pageSize/total (P2.6).
  - **`GET /roles/{id}/permissions`** retorna `{ Id, Permissions }` (shape ad-hoc) enquanto `GET /roles` retorna `TenantRoleView[]` completo — leve inconsistência de contrato.
  - **`POST /vertical-setup/apply`** e **`POST /onboarding/drafts/{id}/complete`** retornam 200 (não 201) mesmo criando recursos — defensável (idempotentes/retomáveis), mas foge do "201 em criação".
  - **`POST /billing/checkout`** retorna 200 com o objeto de checkout (não 201) — aceitável (não cria um recurso REST endereçável do lado do Nexora).
- **`OnboardingConflictException`** inclui `exception.GetType().Name` na mensagem devolvida — vaza o **nome do tipo** de exceção EF (não dados). P3.

---

## 14. FRONTEND

- **Angular 22 strict**, standalone components, signals + `computed`, `@if/@for` control flow. `providePrimeNG` com `@layer` (reset/primeng/nexora).
- **Services:** todos via `HttpClient` (nenhum `fetch`). URL base sempre de `APP_CONFIG.apiBaseUrl` (runtime `config.js` em prod, `environment.development.ts` fixo em dev). **Nenhuma URL nem `TenantId` hardcoded.**
- **Interceptors:** só `auth.interceptor` — anexa `Bearer` **apenas** para requests cujo origin+prefixo batem com a API (`isNexoraApiRequest`), evitando vazar o token para terceiros. ✓
- **Guards:** `authGuard` (refresh se não autenticado), `tenantGuard` (redireciona `/` sem `tenant_id`), `platformAdminGuard` (`/403` se autenticado não-admin, `/login` se não autenticado).
- **Problemas:**
  - **Sem interceptor de erro** → sem 401→refresh→retry, sem toast/handler global (P2.3).
  - `home-page` pede slug digitado (P1.2).
  - `subscription-admin` usa `window.prompt` (P2.8).
  - Campos de UUID crus em `schedule`/`admin` (P2.7).
  - Nenhuma lógica de negócio pesada na view; sem `any` além de `form: any` nos formulários simples (aceitável); sem subscription manual sem cleanup relevante (guards usam `.pipe(map)`; componentes usam `subscribe` em contexto de vida curta ou signals).
  - `NxPagination` sem uso (P3.6).
- **Design system:** **um único** padrão agora (migração DentalFlow concluída) — `styles.scss @layer nexora` (tokens `--nx-*`) + `src/app/shared/ui/` (17 componentes: NxSidebar, NxTopbar, NxPageHeader, NxCard, NxBadge, NxButton, NxStatCard, NxDataTable, NxEmptyState, NxPagination, NxModal, NxConfirmDialog, NxSearchInput, NxChipFilter, NxSwitch, NxAvatar, NxFormField). Bloco `.nx-content` de normalização **removido**. `shared/ui/button/nx-button.ts` antigo (wrapper `p-button`) **deletado** (confirmado no `git status`). PrimeNG praticamente não usado (só `providePrimeNG`/preset). **Sem CSS morto relevante, sem componente duplicado.** Dívida: bundle acima do budget.

---

## 15. SEGURANÇA

| Controle | Estado |
|---|---|
| Secrets no repo | **Nenhum.** `appsettings.json` só placeholders vazios. `.env`/`.env.example` gitignored; dev usa user-secrets. `ProductionConfiguration` derruba o startup sem SigningKey(≥32)/MP/Resend/AppInsights/Cors-HTTPS/AllowedHosts reais. |
| Hash de senha | `IPasswordHasher<User>` (PBKDF2 do ASP.NET Core, parâmetros atualizáveis). |
| JWT | HS256, secret externo ≥32, issuer/audience/assinatura/lifetime validados. Access 15min, em memória na SPA. |
| Refresh token | 7d, aleatório 64 bytes, **só hash SHA-256 persistido**, cookie `HttpOnly` + `Secure`(fora de dev) + `SameSite=Strict` + `Path=/api/v1`. Rotação por uso, detecção de reuso revoga a família. |
| Lockout | `IsActive` no user; sem lockout por tentativas (rate limit `auth` cobre parcialmente). |
| Rate limiting | Nativo. `auth` 10/min, `onboarding` 60/min, `checkout` 10/min, `webhook` 120/min — **particionado por `RemoteIpAddress`** (frágil atrás de proxy — P1.3). 429 + `Retry-After`. |
| CORS | `WithOrigins(config)` + `AllowCredentials`. Dev: `localhost:4200`. Prod: exige HTTPS explícito (validado). |
| Headers | `X-Content-Type-Options:nosniff`, `X-Frame-Options:DENY`, `Referrer-Policy:no-referrer`, `Permissions-Policy` restritivo, `X-Correlation-ID`. HSTS 365d+preload fora de dev. **Sem CSP** (documentado como fora de escopo desta fase). |
| IDOR | `SingleOrDefault(x=>x.TenantId==t && x.Id==id)` → 404 sem revelar existência. FKs compostas. Testado. |
| Webhook | HMAC-SHA256 + tolerância de timestamp + `FixedTimeEquals`; `AllowAnonymous`; idempotência por unique index. |
| Endpoints admin | Grupo único `PlatformAdmin` (rejeita `tenant_id`). |
| Logs | `AddJsonConsole`; `GlobalExceptionHandler` loga só em 500, com `TraceIdentifier`. `Microsoft.EntityFrameworkCore.Database.Command: Warning` em prod (não loga SQL de comandos). **Nunca loga token/senha.** |
| Autorização server-side | Sim, por policy de permissão. **Exceção:** feature não entra na decisão (P1.1); billing sem permissão (P2.1); members list sem permissão (P2.2); deactivate com permissão errada (P1.4). |
| Auditoria | Parcial (P2.9). |
| `dotnet list --vulnerable` | limpo. `npm audit` | 0. |

---

## 16. TESTES

| Suíte | Total | Resultado |
|---|---|---|
| Backend unit (`Nexora.UnitTests`) | 14 | **14 verdes, 0 falha, 0 skip** |
| Backend integração (`Nexora.IntegrationTests`) | 50 | **50 verdes, 0 falha, 0 skip** — rodados contra **PostgreSQL real** (`NEXORA_HARDENING_POSTGRES` → db `nexora_audit_tests` no container) |
| Frontend (`vitest`) | 15 | **15 verdes** |
| **Total** | **79** | **79 verdes** |

**Cobertura relevante presente:** `AuthRbacTokenTests` (gate de RBAC), `TenancyIsolationTests` + `CatalogIsolationTests` + `CustomerAuthorizationTests` (isolamento A↔B, IDOR, TenantId forjado), `TenantAdministrationTests`/`TenantAdministrationPostgresTests` (roles/permissões), `SchedulingTests` (conflito/status/timezone), `SubscriptionTests` (lifecycle), `BillingPaymentTests` (webhook/idempotência), `FeatureAccessTests` (resolver), `ReportsProductivityPostgresTests` + `ReportTests` (bug LINQ **corrigido e verificado**), `PostgresConcurrencyTests`, `OnboardingTests`, `F15MvpJourneyTests` (jornada E2E do MVP), `ProductionHardeningTests` (config de produção).

**Cobertura relevante AUSENTE:**
- **Feature enforcement** — não há teste "plano Básico bloqueia módulo X" nem "limite de N profissionais" (porque o enforcement não existe).
- **Permission negativo em billing/members** — nenhum teste garante que um usuário sem `tenant.manage` **não** vê a assinatura / **não** cria checkout / **não** lista membros.
- **`DeactivateMembershipAsync`** — nenhum teste sobre qual permissão autoriza a desativação.
- **Frontend guards** — `app.routes.spec.ts` cobre `platformAdminGuard`/`tenantGuard`, mas não o fluxo login→sem-tenant→redirect.
- **Contrato/OpenAPI** — sem testes de contrato dedicados (documentado no `LastChanges.md`).

---

## 17. BUILD

| | Comando | Resultado |
|---|---|---|
| Backend | `dotnet build Nexora.slnx -c Release` | **0 warning / 0 erro** (26s) |
| Backend restore | ok | central package management, .NET 10 / EF 10 / Npgsql 10 |
| Frontend | `npm run build` (`ng build`) | **verde**; 1 WARNING de budget (`initial exceeded ... by 219 kB` — dívida documentada) |
| Frontend | `npm test` (`ng test`) | 15/15 |
| `dotnet format --verify-no-changes` | (não executado nesta auditoria — read-only) | `LastChanges.md` registra 6 arquivos pré-existentes fora do padrão |

---

## 18. GIT

- **Branch:** `develop` (up to date com `origin/develop`).
- **Último commit:** `68c3bfa add atualizações` (2026-09-01 19:47) — trabalho de frontend visual (styles.scss +558, vertical-setup etc.). Anterior: `885bc89 commit develop`. Base: `ade4609 feat: implement Nexora MVP foundation through F15`.
- **Working tree — modificados (46):** `backend/src/Nexora.Api/Nexora.Api.csproj` (+`<UserSecretsId>`), `.../launchSettings.json` (porta 8080 pro front dev), e **43 arquivos de frontend** (`app.ts`/`app.html`/`styles.scss` + 40 telas/scss) — tudo da **migração visual DentalFlow** (Fases 1–7, em andamento nas sessões anteriores) + `frontend/.../shared/ui/button/nx-button.ts` **deletado**.
- **Novos não-rastreados (21):** `docs/NEXORA-DENTALFLOW-UI-MIGRATION.md`, `frontend/.../shared/ui/*` (17 componentes + barril + pasta `shell/`), `db-set-password.ps1` e `notes.md` (de outras sessões).
- **Diff:** `46 files changed, 1691 insertions(+), 2110 deletions(-)` (frontend) + 2 linhas de backend config.
- **Trabalho de agentes diferentes:** o working tree é **uma linha de trabalho coerente** (migração visual) + 2 ajustes pontuais de backend (VS/porta 8080, config.js dev). Não há conflito aparente entre agentes; `TenancyEndpoints.cs`/`TenantContextMiddleware.cs` (mexidos numa sessão anterior de RBAC) **já estão commitados** em `ade4609`+`885bc89` — não aparecem no working tree.
- **Removidos:** só `frontend/.../shared/ui/button/nx-button.ts` (wrapper `p-button` sem uso — substituído pelo novo `nx-button.ts`).
- **Nada a "limpar"** — o working tree é trabalho legítimo não commitado.

---

## 19. FASES DO MASTER PLAN

| Fase | Status | Evidência | Pendência |
|---|---|---|---|
| F0 Docs/Arquitetura | **CONCLUÍDA** | `docs/**` + 18 ADRs | — |
| F1 Fundação | **CONCLUÍDA** | 4 projetos + Angular + Docker + health + Swagger + logging JSON | estrutura difere do plano (§5) sem ADR (P3.9) |
| F2 Identity/Segurança | **CONCLUÍDA** | JWT+refresh rotativo, `IdentityFlowTests`, ADR-0007 | recuperação/alteração de senha não existem |
| F3 Multi-tenancy | **CONCLUÍDA** | isolamento manual + FKs compostas + `TenancyIsolationTests` (PG) | sem global filter (P3.1) |
| F4 Platform Admin | **CONCLUÍDA** | grupo `/admin` + `PlatformAdmin` policy + `AuditLog` + `PlatformAdministrationTests` | auditoria não cobre tudo (P2.9) |
| F5 Plans/Features | **CONCLUÍDA** (2026-09-02, ADR-0019) | entidades + `FeatureAccessService` + `RequireFeature` nos 5 grupos + limites com advisory lock + `TenantFeatureOverride` + corte na `Subscription` vencida + `GET /identity/me` features + `featureGuard`/sidebar; `FeatureEnforcementTests`/`FeatureEnforcementPostgresTests` | `AuditLog` de override/plan/feature (P2.9), não bloqueante |
| F6 Customers | **CONCLUÍDA** | CRUD + busca + paginação + status, tenant-scoped, testado | — |
| F7 Professionals/Services | **CONCLUÍDA** | CRUD + vínculo + FKs compostas, `CatalogIsolationTests` | sem paginação (P2.6) |
| F8 Scheduling | **CONCLUÍDA** | working hours + bloqueios + appointments + conflito + status + timezone + advisory lock, `SchedulingTests` | UUID crus na UI (P2.7) |
| F9 Billing/Subscriptions | **CONCLUÍDA** | `Subscription` + lifecycle (Trialing/Active/PastDue/Cancelled/Expired) + `PersistentTenantPlanProvider` + eventos, `SubscriptionTests` | billing sem gate de permissão (P2.1) |
| F10 Payments | **CONCLUÍDA** | `IPaymentGateway` + MercadoPago + webhook HMAC + idempotência, `BillingPaymentTests` | `eventId` fallback frágil (P3.7) |
| F11 Notifications | **CONCLUÍDA** | `IEmailSender` (Resend/Fake) + outbox PG + worker + retry + WelcomeEmail, `NotificationTests` | Resend real não validado (doc já diz "não bloqueante") |
| F12 Reports | **CONCLUÍDA** | `/reports/overview` tenant + `/admin/reports/overview` plataforma; **bug LINQ de produtividade CORRIGIDO** e travado por `ReportsProductivityPostgresTests` | — |
| F13 Onboarding | **CONCLUÍDA** | wizard 5 passos + `CompleteAsync` transacional (SERIALIZABLE + FOR UPDATE) + trial, `OnboardingTests` | após logout+login não reentra (P1.2) |
| F14 Hardening | **PARCIAL** | HSTS/headers/rate-limit/forwarded-headers/CI(`ci.yml`,`deploy-production.yml`)/`ProductionConfiguration` no código | defaults de proxy inseguros e não validados (P1.3); provisionamento real pendente (doc: "não bloqueante") |
| F15 Primeiro vertical | **CONCLUÍDA** | `VerticalTemplates:BARBERSHOP_SALON` por config + migration `SeedBarbershopSalonVertical` + `VerticalSetupTests` | — |

---

## 20. TOP 10 PROBLEMAS (mais grave → menos grave)

1. **P1.1** — Planos/Features não são aplicados em runtime (a promessa central do SaaS não existe).
2. **P1.3** — Defaults de proxy reverso inseguros e não validados → rate limiter de login vira global em produção.
3. **P1.4** — Desativar membro da equipe é gateado em `customers.delete` (permissão errada).
4. **P1.2** — Usuário que retorna não consegue reentrar na empresa (sem "minhas empresas", slug digitado).
5. **P2.1** — Billing (`/subscription`, `/billing/checkout`) sem gate de permissão — qualquer membro cria checkout.
6. **P2.9** — Auditoria não cobre mudança de papéis/permissões, desativação de membro, ações de subscription.
7. **P2.2** — `GET /tenant/members` sem permissão — qualquer membro lista e-mails de todos.
8. **P2.3** — Frontend sem interceptor de erro/refresh — sessão "quebra" em 15min até reload.
9. **P2.6** — Listagens sem paginação exceto `/customers` (risco de performance na escala).
10. **P3.1** — Sem EF global query filter (defesa-em-profundidade ausente para o isolamento).

---

## 21. ORDEM RECOMENDADA DE CORREÇÃO

**P0:** nenhum.

**P1 (nesta ordem, com dependências):**

```
P1.3 (proxy/rate-limit defaults)      ─ independente, rápido, alto risco em prod
      │
P1.4 (permissão de deactivate)        ─ independente, 1 linha + teste
      │
P1.2 (GET /me/tenants + auto-select)  ─ desbloqueia UX de retorno
      │      └─> corrige P2.5 (redirect /clientes→/) de quebra
      ▼
P1.1 (feature enforcement)            ─ o maior; depende de decisão:
      ├─ 1. definir o mapa feature→módulo (REPORTS→/reports, etc.) [ADR]
      ├─ 2. IFeatureGate / RequireFeature nos endpoints + limites em Create*
      ├─ 3. expor features efetivas ao frontend (GET /identity/me ou /features)
      └─ 4. gatear a sidebar por feature (app.ts navGroups)
```

**P2:**
```
P2.1 (billing permission)  ─┐
P2.2 (members permission)   ├─ mesma natureza (adicionar policy), fazer juntos
P2.9 (auditoria)           ─┘ (gravar AuditLog em role/permission/member/subscription)
P2.3 (error interceptor)   ─ independente (frontend)
P2.8 (window.prompt→modal) ─ independente (frontend)
P2.6 (paginação)           ─ independente; frontend NxPagination já pronto (P3.6 resolve junto)
P2.7 (UUID→selects)        ─ depende de P1.2 (Admin precisa do list de tenants) para a parte de Admin
```

**P3:** oportunista, junto com trabalho na área (format, extração de `UserId`, split de `TenancyEndpoints`, global query filter, budget do bundle).

---

## 22. PONTO ATUAL DO PROJETO

- **Última fase realmente concluída:** F15 (primeiro vertical) — e F0–F4, F6–F13 também concluídas.
- **Fases parcialmente concluídas:** **F5** (Plans/Features — catálogo/resolver prontos, enforcement ausente) e **F14** (Hardening — código pronto, defaults de proxy inseguros + provisionamento pendente).
- **Funcionalidade mais avançada / madura:** o núcleo operacional tenant-scoped (Clientes, Profissionais, Serviços, Agenda) — CRUD completo, isolamento testado em PG real, FKs compostas, concorrência com advisory lock, timezone por tenant.
- **Maior bloqueio atual:** o **feature enforcement (P1.1)** — sem ele, "planos" e "limites" são só telas de admin sem efeito, e não dá para vender planos diferenciados. Em segundo lugar, a **reentrada de tenant (P1.2)** — um usuário que fez logout não volta pra própria empresa sem decorar o slug.

---

## 23. VEREDITO

**O Nexora está arquiteturalmente consistente?** → **PARCIALMENTE.**
A arquitetura de base é sólida e coerente: Clean Architecture (direção de dependência testada), Modular Monolith, tenancy resolvido no servidor, RBAC por permissão, separação Platform/Tenant, Billing SaaS separado de Payments. **Mas** a decisão nº 13 do MASTER_PLAN ("Planos e features configuráveis... controlam o acesso") e o critério da F5 não têm implementação em runtime — o resolver existe e ninguém o chama. Isso é uma inconsistência entre a arquitetura documentada e o código.

**O multi-tenancy está seguro?** → **SIM.**
Isolamento consistente em todos os serviços + FKs compostas `(TenantId, Id)` que tornam referência cross-tenant estruturalmente impossível + `ITenantContext` imutável resolvido de claim server-side + testes de isolamento nas duas direções (A-não-lê/altera/exclui-B, IDOR com id válido, TenantId forjado rejeitado) passando em PostgreSQL real. **Ressalva:** não há EF global query filter, então a segurança de um serviço **futuro** depende de disciplina (P3.1). Nada indica vazamento no código atual.

**O RBAC está consistente entre backend e frontend?** → **SIM.**
O gate suspeito foi corrigido: o JWT carrega as permissões de tenant quando um tenant é selecionado, o `TenantContextMiddleware` revalida e troca as claims por requisição antes das policies, e `identity.profile` sobrevive. `AuthRbacTokenTests` (3 casos) trava o comportamento e passa. A sidebar (`has('customers.read')`) enxerga as permissões corretas. Único descompasso: mudança de papel só reflete no JWT no próximo refresh (a API é imediata).

**Plans/Features realmente controlam os módulos?** → **NÃO.**
`IFeatureAccessService.ResolveAsync` e `PersistentTenantPlanProvider` funcionam e são testados isoladamente, mas **nenhum endpoint, guard ou tela os consome**. Nenhum limite de plano é verificado. Desativar um plano/feature no admin não muda nada.

**É seguro continuar adicionando funcionalidades antes das correções?** → **SIM, com ressalva.**
Não há bloqueio de segurança, de isolamento de tenant, de corrupção de dados ou de billing. O build e os 79 testes passam. **Porém:**
- Qualquer feature que **dependa de plano/limite** (ex.: "plano X libera módulo Y") deve esperar **P1.1**.
- Qualquer feature de **UX de conta/empresa** deve esperar **P1.2**.
- Antes de qualquer **deploy de produção**, resolver **P1.3** (proxy/rate-limit).
- **P1.4** (1 linha) deve ser corrigido antes de liberar papéis customizados para clientes reais.

---

# PROMPT PARA PRÓXIMA ETAPA

```
TAREFA — CORRIGIR SOMENTE OS PROBLEMAS P0 E P1 DA AUDITORIA NEXORA

Contexto: docs/AUDITORIA-CONSISTENCIA-2026-09-02.md.
Nesta etapa, corrija APENAS os itens abaixo. Não toque em P2/P3. Não faça a migração visual.
Não commit, não push, sem operação destrutiva de banco. Toda mudança de banco = migration.
Toda regra nova = teste. Rode build + testes ao final e reporte.

P0: nenhum.

P1.3 — Defaults de proxy reverso inseguros
- Em Nexora.Api/Configuration/ProductionConfiguration.cs: quando Production, exigir pelo menos
  um valor em ReverseProxy:KnownProxies OU ReverseProxy:KnownNetworks (erro no startup se ambos vazios).
- Documentar em docs/operations/deployment.md o valor por ambiente (rede do container / IP do Front Door).
- Teste: estender ProductionHardeningTests para cobrir "sem KnownProxies/KnownNetworks em Production → startup falha".
- NÃO alterar o comportamento em Development/Testing.

P1.4 — Permissão errada em desativação de membro
- Em Nexora.Infrastructure/Tenancy/TenancyService.cs, método DeactivateMembershipAsync:
  trocar a checagem de TenantPermissions.CustomersDelete por TenantPermissions.TenantManage.
- Idealmente mover o gate para o endpoint (PATCH /api/v1/tenant/members/{id}/deactivate) como
  RequireAuthorization(TenantPermissions.TenantManage), alinhando com o grupo admin /api/v1/tenant.
- Teste: um papel só com customers.delete NÃO consegue desativar membro (403/404);
  um papel com tenant.manage consegue.

P1.2 — Reentrada de tenant (usuário que retorna)
- Novo endpoint GET /api/v1/me/tenants (RequireAuthorization apenas): lista as empresas ativas do
  usuário autenticado — { id, name, slug, roleName } — usando o índice (UserId, IsActive) de tenant_users.
  NÃO expor tenants inativos nem vínculos inativos.
- Frontend:
  - AuthService: método listMyTenants().
  - Após login bem-sucedido em auth-page.ts: se a lista tiver exatamente 1 tenant, chamar
    auth.selectTenant(slug) automaticamente antes de navegar; se tiver >1, navegar para um seletor;
    se 0, manter o fluxo atual (criar empresa).
  - home-page.ts: substituir o input de slug digitado por uma lista clicável das empresas do usuário
    (com opção "Criar empresa"). Manter o fallback de digitação só se a lista falhar.
- Isso também resolve P2.5 (redirect /clientes→/).
- Testes: integração do endpoint (isolamento — usuário A não vê tenants de B); frontend spec do
  auto-select com 1 tenant.

P1.1 — Enforcement de Plans/Features (o maior — fazer por último, com ADR)
- PASSO 1 (decisão + ADR): criar docs/decisions/ADR-00XX-feature-enforcement.md definindo o mapa
  feature→módulo/endpoint (ex.: CUSTOMERS→/customers, SCHEDULING→/appointments, REPORTS→/reports,
  PROFESSIONALS→/professionals, SERVICES→/services) e o comportamento quando a feature está desabilitada
  (403 com problem+json "feature_not_in_plan") e quando um limite é atingido (409/422).
  Decidir se módulos "core" (customers/professionals/services/scheduling) são sempre habilitados
  ou também dependem de feature.
- PASSO 2 (backend): criar um mecanismo reutilizável — sugestão: um endpoint filter
  .RequireFeature("REPORTS") que resolve via IFeatureAccessService.ResolveAsync(tenantContext.TenantId, code)
  e rejeita com 403 se !Enabled. Aplicar aos grupos de endpoint conforme o ADR.
- PASSO 3 (limites): em CustomerService.CreateAsync e CatalogService.CreateProfessionalAsync,
  quando FeatureAccess.Limit != null, contar os registros ativos do tenant e rejeitar (409/422) se >= Limit.
- PASSO 4 (frontend): expor as features/limites efetivos — adicionar ao GET /api/v1/identity/me
  (quando há tenant) OU um GET /api/v1/features tenant-scoped; no app.ts navGroups, gatear cada item
  também pela feature correspondente.
- Testes OBRIGATÓRIOS:
  - tenant sem a feature no plano → GET do módulo retorna 403;
  - override do tenant habilita/desabilita e o endpoint respeita imediatamente;
  - limite: criar até o limite passa, o próximo falha;
  - plano desativado (subscription expirada) → módulos não-core bloqueiam.

REGRAS
- Preservar todo o trabalho não commitado (migração visual DentalFlow) — não reverter, não descartar.
- Não alterar contratos de endpoints existentes além do necessário (P1.1 adiciona 403; P1.2 adiciona rota).
- Não mexer em multi-tenancy além do endpoint novo de P1.2.
- Migrations só se P1.1 exigir (não deve — as entidades já existem).
- Ao final: dotnet build + dotnet test (com NEXORA_HARDENING_POSTGRES) + ng build + ng test; reportar
  arquivos alterados, migrations, resultado dos testes e pendências. Não commitar.
```
