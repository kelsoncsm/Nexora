RELATÓRIO FINAL — Refatoração estrutural dos Minimal API Endpoints

INVENTÁRIO

Arredondado por nº de rotas e responsabilidades (grep -E 'Map(Get|Post|Put|Patch|Delete|Group)' em backend/src/Nexora.Api):

┌───────────────────────────────────────────┬───────┬────────────────────────────────────────────────────────────────────────────────────────────────────────────────┬─────────────────────────────────────┐
│                  Arquivo                  │ Rotas │                                               Responsabilidades                                                │                Ação                 │
├───────────────────────────────────────────┼───────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼─────────────────────────────────────┤
│ Administration/AdministrationEndpoints.cs │    18 │ Dashboard, Tenants, Users, Segments, Audit, Features, Plans, Feature Overrides (2 services)                    │ DIVIDIDO                            │
├───────────────────────────────────────────┼───────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼─────────────────────────────────────┤
│ Billing/BillingEndpoints.cs               │    18 │ subscription (tenant), billing invoices/checkout (tenant), webhook MP (anon), admin/subscriptions (10),        │ DIVIDIDO                            │
│                                           │       │ admin/billing prices+invoices+payments                                                                         │                                     │
├───────────────────────────────────────────┼───────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼─────────────────────────────────────┤
│ Catalog/CatalogEndpoints.cs               │    11 │ Professionals (7), Services (4) — arquivo em 1 linha                                                           │ DIVIDIDO                            │
├───────────────────────────────────────────┼───────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼─────────────────────────────────────┤
│ Scheduling/SchedulingEndpoints.cs         │    11 │ context, working-hours (2), blocked-periods (3), appointments (5)                                              │ DIVIDIDO                            │
├───────────────────────────────────────────┼───────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼─────────────────────────────────────┤
│ Tenancy/TenancyEndpoints.cs               │    15 │ tenant público/sessão/membros + grupo admin /tenant (perfil, roles, permissions)                               │ NÃO ALTERADO (ver abaixo)           │
├───────────────────────────────────────────┼───────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼─────────────────────────────────────┤
│ Customers/CustomerEndpoints.cs            │     5 │ Customers — arquivo em 1 linha                                                                                 │ REFORMATADO (sem split — recurso    │
│                                           │       │                                                                                                                │ único)                              │
├───────────────────────────────────────────┼───────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼─────────────────────────────────────┤
│ Identity/IdentityEndpoints.cs             │     5 │ Identity/auth                                                                                                  │ Já no padrão-alvo — nada a fazer    │
├───────────────────────────────────────────┼───────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼─────────────────────────────────────┤
│ Onboarding/OnboardingEndpoints.cs         │     6 │ Onboarding                                                                                                     │ Coeso, recurso único, legível —     │
│                                           │       │                                                                                                                │ nada a fazer                        │
├───────────────────────────────────────────┼───────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼─────────────────────────────────────┤
│ Reports/ReportEndpoints.cs                │     2 │ tenant + platform report                                                                                       │ Trivial — nada a fazer              │
├───────────────────────────────────────────┼───────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────┼─────────────────────────────────────┤
│ Verticals/VerticalSetupEndpoints.cs       │     2 │ vertical-setup                                                                                                 │ Trivial — nada a fazer              │
└───────────────────────────────────────────┴───────┴────────────────────────────────────────────────────────────────────────────────────────────────────────────────┴─────────────────────────────────────┘

---

REFATORADOS

1. AdministrationEndpoints (18 → agregador + 8 submódulos)

ANTES
Administration/AdministrationEndpoints.cs
└── 18 endpoints, 8 responsabilidades, 2 services, lambdas comprimidas, 7 records privados

DEPOIS
Administration/
├── AdministrationEndpoints.cs          (agregador: MapGroup "/api/v1/admin"
│                                        + RequireAuthorization("PlatformAdmin")
│                                        + WithTags("Platform Administration")
│                                        + 7 internal sealed record de request)
├── DashboardEndpoints.cs               GET /dashboard
├── TenantAdminEndpoints.cs             GET /tenants · GET /tenants/{id} · PATCH /tenants/{id}/status
├── UserAdminEndpoints.cs               GET /users
├── SegmentEndpoints.cs                 GET/POST /segments · PUT /segments/{id}
├── AuditEndpoints.cs                   GET /audit-logs
├── FeatureEndpoints.cs                 GET/POST /features · PUT /features/{id}
├── PlanEndpoints.cs                    GET/POST /plans · PUT /plans/{id} · PUT /plans/{planId}/features/{featureId}
└── TenantFeatureOverrideEndpoints.cs   GET/PUT /tenants/{tenantId}/feature-overrides[/{featureId}]

2. BillingEndpoints (18 → agregador + 4 submódulos)

ANTES
Billing/BillingEndpoints.cs
└── 18 endpoints, 5 grupos, 3 políticas distintas, tudo em linha comprimida

DEPOIS
Billing/
├── BillingEndpoints.cs           (agregador: delega — grupos têm prefixos/políticas diferentes)
├── TenantBillingEndpoints.cs     GET /api/v1/subscription  +  grupo /api/v1/billing (GET /invoices, POST /checkout)   [claim tenant_id]
├── PaymentWebhookEndpoints.cs    POST /api/v1/payments/webhooks/mercado-pago   [AllowAnonymous, rate-limit "webhook"]
├── SubscriptionAdminEndpoints.cs grupo /api/v1/admin/subscriptions — 10 rotas   [PlatformAdmin]
└── BillingAdminEndpoints.cs      grupo /api/v1/admin/billing — prices/invoices/payments   [PlatformAdmin]

3. CatalogEndpoints (11 → agregador + 2)

ANTES  Catalog/CatalogEndpoints.cs  (1 linha, 11 rotas: professionals + services)
DEPOIS Catalog/
       ├── CatalogEndpoints.cs        (agregador)
       ├── ProfessionalEndpoints.cs   grupo /api/v1/professionals — 7 rotas
       └── ServiceEndpoints.cs        grupo /api/v1/services — 4 rotas

4. SchedulingEndpoints (11 → agregador + 3)

ANTES  Scheduling/SchedulingEndpoints.cs  (1 método, 11 rotas, 4 sub-recursos comprimidos)
DEPOIS Scheduling/
       ├── SchedulingEndpoints.cs       (agregador + GET /api/v1/scheduling/context)
       ├── WorkingHoursEndpoints.cs     grupo /api/v1/working-hours — 2 rotas
       ├── BlockedPeriodEndpoints.cs    grupo /api/v1/blocked-periods — 3 rotas
       └── AppointmentEndpoints.cs      grupo /api/v1/appointments — 5 rotas

5. CustomerEndpoints — reformatado

De uma única linha para método legível com handlers GetAsync/CreateAsync/UpdateAsync/DeleteAsync extraídos. Sem split (recurso único, regra 16).

Padrões aplicados

- Agregador só monta MapGroup + auth + tags e chama x.MapYyyEndpoints().
- Submódulos = internal static class com public static void MapYyyEndpoints(this IEndpointRouteBuilder).
- Lambdas comprimidas → métodos: todo endpoint com branch (is { } x ? Ok() : NotFound()), Results.Created(...) ou UserId(user) virou private static async Task<IResult> nomeado. Pass-through puro ((svc, ct) => svc.XAsync(ct)) ficou como lambda formatada (regra 15/16 — extrair delegações idênticas seria ruído).
- Records de request: internal sealed record no agregador (Administration) ou no submódulo dono (ChangePlanInput, WebhookInput/WebhookData). Nenhum arquivo DTO novo.
- UserId(ClaimsPrincipal): mantido como private static duplicado nos submódulos que usam (mesmo padrão já existente no projeto). Não foi criado helper de identidade compartilhado — mexeria em resolução de claims (fora do escopo, regra 10).

---

ROTAS

Nenhuma rota pública foi alterada. Verificação por 3 vias:

1. Multiset de Map<Verbo>("path") (git HEAD × working tree) via script tolerante a quebra de linha:
OLD count 63  ·  NEW count 63  ·  IDENTICAL route+verb multiset
2. Prefixos de grupo × políticas × tags (grep | sort | uniq -c): idêntico — 11 MapGroup, 3 RequireAuthorization("PlatformAdmin"), 2 RequireClaim("tenant_id"), todos os TenantPermissions.* na mesma contagem, AllowAnonymous 1×, RequireRateLimiting "checkout"+"webhook".
3. Swagger (GET /swagger/v1/swagger.json com a API no ar): 72 paths / 93 operations gerados sem erro; contagem por tag confere (Platform Administration 18, Billing Administration 14, Scheduling 11, Professionals 7, Services 4, Customers 5, Billing 3, Payment Webhooks 1…).

Startup da API: sem warning de rota duplicada/ambígua.

---

AUTORIZAÇÃO

Preservada integralmente:

┌──────────────────────────────────────────────────────────────────────────────────────┬──────────────────────────────────────────────────────────────────┬────────────┬────────────┐
│                                        Escopo                                        │                             Política                             │   Antes    │   Depois   │
├──────────────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────┼────────────┼────────────┤
│ Platform admin (/api/v1/admin/*, /api/v1/admin/subscriptions, /api/v1/admin/billing) │ RequireAuthorization("PlatformAdmin")                            │ 3×         │ 3×         │
├──────────────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────┼────────────┼────────────┤
│ Billing do tenant (/api/v1/subscription, /api/v1/billing)                            │ RequireAuthorization(policy => policy.RequireClaim("tenant_id")) │ 2×         │ 2×         │
├──────────────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────┼────────────┼────────────┤
│ Webhook Mercado Pago                                                                 │ AllowAnonymous() + RequireRateLimiting("webhook")                │ 1×         │ 1×         │
├──────────────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────┼────────────┼────────────┤
│ Catalog / Scheduling / Customers                                                     │ RequireAuthorization(TenantPermissions.Xxx) por rota             │ inalterado │ inalterado │
├──────────────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────────────────────────────────┼────────────┼────────────┤
│ Checkout                                                                             │ RequireRateLimiting("checkout")                                  │ 1×         │ 1×         │
└──────────────────────────────────────────────────────────────────────────────────────┴──────────────────────────────────────────────────────────────────┴────────────┴────────────┘

Nenhum RequireAuthorization removido, afrouxado ou movido. Nenhuma decisão de autorização manual (if user.Role == ...) introduzida.

---

MULTI-TENANCY

Nenhuma regra de isolamento de tenant modificada:

- ITenantContext / tenant.TenantId / tenant.IsAvailable passados aos services exatamente como antes.
- RequireClaim("tenant_id") preservado nos endpoints de billing do tenant.
- TenantContextMiddleware.cs não foi tocado (aparece no git status por trabalho anterior seu).
- TenancyEndpoints.cs não foi tocado — deliberadamente (ver PONTOS NÃO ALTERADOS).
- Os endpoints continuam finos: zero regra de negócio movida da camada Application para a API.

---

BUILD

Comando:   dotnet build backend/Nexora.slnx
Baseline:  Compilação com êxito · 0 Aviso(s) · 0 Erro(s)
Depois:    Compilação com êxito · 0 Aviso(s) · 0 Erro(s)

---

TESTES

Comando:   dotnet test backend/Nexora.slnx --no-build   (container nexora-postgres-1 no ar)

Baseline:  Nexora.UnitTests          14/14  ✅
           Nexora.IntegrationTests   43 (não-PG) + 7 (PG) = 50/50  ✅   → total 64

Depois:    Nexora.UnitTests          14/14  ✅
           Nexora.IntegrationTests   50/50  ✅                          → total 64

Sem regressões. Os testes de integração (PlatformAdministrationTests, SubscriptionTests, BillingPaymentTests, CatalogIsolationTests, SchedulingTests, CustomerAuthorizationTests, AuthRbacTokenTests, F15MvpJourneyTests…) exercem as rotas reais via HTTP e cobrem o contrato refatorado.

Não há testes de contrato/OpenAPI dedicados no projeto — supri isso com a checagem manual do swagger.json.

---

GIT

Branch master (mesma do trabalho não-commitado anterior — preservado, nada resetado). Nenhum commit feito.

CRIADOS (17):
  Administration/DashboardEndpoints.cs
  Administration/TenantAdminEndpoints.cs
  Administration/UserAdminEndpoints.cs
  Administration/SegmentEndpoints.cs
  Administration/AuditEndpoints.cs
  Administration/FeatureEndpoints.cs
  Administration/PlanEndpoints.cs
  Administration/TenantFeatureOverrideEndpoints.cs
  Billing/TenantBillingEndpoints.cs
  Billing/PaymentWebhookEndpoints.cs
  Billing/SubscriptionAdminEndpoints.cs
  Billing/BillingAdminEndpoints.cs
  Catalog/ProfessionalEndpoints.cs
  Catalog/ServiceEndpoints.cs
  Scheduling/AppointmentEndpoints.cs
  Scheduling/WorkingHoursEndpoints.cs
  Scheduling/BlockedPeriodEndpoints.cs

MODIFICADOS (5 por esta tarefa):
  Administration/AdministrationEndpoints.cs   (→ agregador + records)
  Billing/BillingEndpoints.cs                 (→ agregador)
  Catalog/CatalogEndpoints.cs                 (→ agregador)
  Scheduling/SchedulingEndpoints.cs           (→ agregador + rota context)
  Customers/CustomerEndpoints.cs              (reformatado, sem split)

REMOVIDOS: nenhum (arquivos-agregadores mantiveram nome e assinatura pública
           MapXxxEndpoints — Program.cs não mudou)

NÃO TOCADOS por mim (aparecem no git status por trabalho anterior):
  Tenancy/TenancyEndpoints.cs, Tenancy/TenantContextMiddleware.cs, e todo o resto

Program.cs não foi alterado — os pontos de entrada (app.MapAdministrationEndpoints() etc.) continuam iguais.

---

PONTOS NÃO ALTERADOS

1. TenancyEndpoints.cs (15 rotas, candidato a split: tenancy pública × administração do tenant) — deixado intacto porque: (a) já estava M no working tree (trabalho seu em andamento) — refatorar agora geraria conflito de merge; (b) rule 19 pede pausa em mudanças que tocam superfície de multi-tenancy; (c) já tem handlers extraídos (não está comprimido). Recomendação futura: dividir em PublicTenancyEndpoints (resolve slug, create, session, members) + TenantAdministrationEndpoints (grupo /api/v1/tenant), quando o seu trabalho pendente nele estabilizar.
2. Tag [TenancyEndpoints] no Swagger — 3 rotas (GET /tenant/members, GET /t/{slug}/members, PATCH /tenant/members/{id}/deactivate) não têm .WithTags(...), então o Swashbuckle usa o nome da classe como tag. É pré-existente em TenancyEndpoints.cs. Corrigir junto com o item 1.
3. UserId(ClaimsPrincipal) duplicado — o helper Guid.Parse(FindFirstValue(NameIdentifier) ?? FindFirstValue("sub") ?? throw) existe agora em ~7 arquivos (era 4). Consolidar num internal static ClaimsPrincipalExtensions.UserId(...) seria a limpeza correta, mas mexe em resolução de identidade → fora do escopo desta refatoração estrutural (regra 10). Oportunidade registrada.
4. dotnet format project-wide NÃO executado — dotnet format backend/src/Nexora.Api --verify-no-changes acusa violações pré-existentes em 6 arquivos fora do escopo (Program.cs, ServiceCollectionExtensions.cs, GlobalExceptionHandler.cs, IdentityEndpoints.cs, TenancyEndpoints.cs, TenantContextMiddleware.cs — ordenação de using e whitespace). Rodar o format tocaria todos eles (regra 14: "não provocar alterações massivas fora do escopo"). Meus 17 arquivos novos + 5 modificados passam limpos no dotnet format --verify-no-changes — já foram escritos no padrão do .editorconfig.
5. window.prompt / lógica frouxa — nenhum problema funcional ou de segurança novo encontrado nos endpoints refatorados. Os endpoints "process/activate/cancel" de subscription não são auditados individualmente além do context.TraceIdentifier que já passavam — comportamento preservado, não é regressão.

● Ran 1 stop hook
  ⎿  Stop hook feedback: Lembrete automatico (Segundo Cerebro): antes de encerrar, verifique a Regra 2 de C:\SEGUNDO CEREBRO\_Sistema\INSTRUCOES-AUTOMACAO.md - se esta entrega gerou decisao, padrao, preferencia, stack ou info de projeto nova, registre agora no cofre, sem perguntar e sem avisar que vai fazer.

  Wrote 2 memories

Registrado no cofre durante a tarefa: o padrão [[Dividir arquivo de Minimal API grande por responsabilidade (agregador + submódulos)]] (+ índice + Log de Sessões) e a memória local de build/test do backend. Nada mais a gravar.

Resumo: 5 arquivos de endpoints refatorados (Administration 18→8, Billing 18→4, Catalog 11→2, Scheduling 11→1+3, Customer reformatado), 17 arquivos novos, contrato HTTP preservado byte-a-byte (63 rotas idênticas, auth/tags/rate-limiting idênticos, swagger OK), build sem warnings, 64 testes verdes. TenancyEndpoints deixado intacto de propósito (já em edição + superfície de tenancy). Sem commit.