# Nexora — Cobertura de UI (auditoria + checklist)

> **Referência visual oficial:** DentalFlow (`C:\Users\Kelso\source\repos\dentalflow\frontend`).
> **Sistema destino:** Nexora (`frontend/nexora-web`, Angular 22 + PrimeNG 22).
> Migração visual anterior: `docs/NEXORA-DENTALFLOW-UI-MIGRATION.md` (Fases 1–7 — shell + DS + 18 telas).
>
> Documento vivo. Atualizar a cada fase. Criado em 2026-09-02 (Fase A da tarefa "Completar o frontend").

---

## 0. Resumo executivo

| Dimensão | Situação |
|---|---|
| Design System | `src/styles.scss` `@layer nexora` + `src/app/shared/ui/` (17 componentes). Alinhado ao DentalFlow. |
| Tema | **Somente CLARO.** Alternador de tema removido (2026-09-02). Sem dark mode, sem `prefers-color-scheme`. |
| Sidebar | Hoje **navy** (`#171b2e`). Decisão nova: **migrar para sidebar clara** (Fase B). |
| Telas de tenant | 18 migradas ao DS; faltam refinamentos e telas de administração. |
| Administração de plataforma | 1 tela (`/admin`) com switcher interno cru — **não** segue o padrão "Configurações" do DentalFlow. |
| Granularidade de permissão | **CRUD real existe** para `customers/professionals/services/appointments` e para **`tenant.members.*`** (2026-09-03). `reports` = só `read`. Perfil/papéis/billing da empresa = `tenant.manage` (sem CRUD ainda). Plataforma = papel `PlatformAdmin` (sem permissões finas). |
| Gaps bloqueantes de arquitetura | Nenhum. Ver §7 (split restante de `tenant.manage` em profile/roles/billing e granularidade da plataforma). |

---

## 1. Catálogo real de permissões (backend — fonte da verdade)

`Nexora.Application/Tenancy/TenancyContracts.cs` → `TenantPermissions` / `TenantPermissions.Catalog`
(exposto em `GET /api/v1/tenant/permissions`, campo `{ key, module, action }`).

| Módulo | Ações existentes | Observação |
|---|---|---|
| `customers` | `read` · `create` · `update` · `delete` | CRUD completo |
| `professionals` | `read` · `create` · `update` · `delete` | CRUD completo (+ link/unlink de serviço usam `update`) |
| `services` | `read` · `create` · `update` · `delete` | CRUD completo |
| `appointments` | `read` · `create` · `update` · `cancel` | sem `delete` — cancelamento no lugar de exclusão |
| `reports` | `read` | só leitura |
| `tenant` | `manage` | **gate único** de toda a administração da empresa (perfil, papéis, permissões, membros, billing) |

Papéis (`TenantRole`): **`ADMIN`** (`IsSystem`, imutável, todas as permissões) + papéis custom por tenant, com
lista livre de `permissionKey`. Papéis custom são editáveis em `/perfis` e `/permissoes`.

Plataforma: policy **`PlatformAdmin`** (claim `platform.access` **e ausência** de `tenant_id`). Não há
permissões finas — é tudo-ou-nada. Claims documentadas: `platform.access`, `platform.tenants.manage`,
`platform.users.read`, `platform.segments.manage`, `platform.audit.read` (hoje concedidas em bloco).

Features/módulos (`FeatureCodes`): `CUSTOMERS`, `PROFESSIONALS`, `SERVICES`, `SCHEDULING`, `REPORTS`
(gate de plano via `RequireFeature` + `featureGuard`). Billing **não** é feature-gated (ADR-0019).

---

## 2. Matriz Backend × UI

Legenda de status: **EXISTE** · **INCOMPLETA** (tela existe, falta função/telas-filhas) ·
**PADRÃO ANTIGO** (existe mas fora do padrão DentalFlow) · **SEM TELA** · **SEM MENU** · **N/A** (endpoint técnico).

### 2.1 Identidade / Sessão / Tenancy

| Funcionalidade | Endpoint | Tela Nexora | Menu | Permissão | Status |
|---|---|---|---|---|---|
| Registro | `POST /identity/register` | `/cadastro` (AuthPage) | — | anon | EXISTE (login aprovado) |
| Login | `POST /identity/login` | `/login` | — | anon | EXISTE (aprovado) |
| Refresh / Logout | `POST /identity/refresh` `/logout` | interceptor | — | anon | N/A |
| Perfil do usuário | `GET /identity/me` | `/perfil` (ProfilePage) | dropdown | `identity.profile` | EXISTE · **INCOMPLETA** (só leitura; sem trocar senha, sem preferências) |
| Resolver tenant público | `GET /t/{slug}` | página pública de tenant | — | anon | SEM TELA (baixa prioridade) |
| Criar empresa | `POST /tenants` | `/onboarding`, `/` | fluxo | auth | EXISTE |
| Entrar numa empresa | `POST /t/{slug}/session` | `/selecionar-empresa`, `/` | fluxo | auth | EXISTE |
| Minhas empresas | `GET /me/tenants` | `/selecionar-empresa`, `/` | fluxo | auth | EXISTE |
| Trocar de empresa (estando logado) | idem | — | — | auth | **INCOMPLETA** (hoje exige logout; sem seletor no shell) |

### 2.2 Administração da empresa (tenant) — gate `tenant.manage`

| Funcionalidade | Endpoint | Tela Nexora | Menu | Status |
|---|---|---|---|---|
| Ver/editar dados da empresa | `GET/PUT /tenant` | `/empresa` (EmpresaPage) | hub Configurações | EXISTE · INCOMPLETA (sem logo, sem endereço/CNPJ — backend só tem nome + timezone) |
| Catálogo de permissões | `GET /tenant/permissions` | `/permissoes` | hub | EXISTE |
| Listar papéis | `GET /tenant/roles` | `/perfis` (RolesPage) | hub | EXISTE |
| Criar/editar papel | `POST/PUT /tenant/roles` | `/perfis` (NxModal) | hub | EXISTE (nome+descrição) |
| Definir permissões de papel | `PUT /tenant/roles/{id}/permissions` | `/permissoes` | hub | EXISTE · rejeita conjunto fora da autoridade do ator (403, regra de não escalonamento — não dá para se auto-promover editando um papel) |
| Ver permissões do papel | `GET /tenant/roles/{id}/permissions` | `/permissoes` | hub | EXISTE (checkboxes por módulo) |
| Definir permissões do papel | `PUT /tenant/roles/{id}/permissions` | `/permissoes` | hub | EXISTE · **INCOMPLETA** (uma role por vez; falta a **matriz** perfil × módulo × ação pedida) |
| Listar membros | `GET /tenant/members` | `/equipe` (TeamPage) | GESTÃO | EXISTE · gate `tenant.members.read` |
| Atribuir papel a membro | `PATCH /tenant/members/{id}/role` | `/equipe` | GESTÃO | EXISTE · gate `tenant.members.update` + regra de não escalonamento |
| Desativar membro | `PATCH /tenant/members/{id}/deactivate` | `/equipe` (NxConfirmDialog) | GESTÃO | EXISTE · gate `tenant.members.delete` |
| **Convidar usuário na empresa** | `POST /tenant/members/invitations` (+ `resend`, `DELETE`, `accept` anônimo, `assignable-roles`) | `/equipe` modal "Incluir usuário" + convites pendentes + `/convite/aceitar` | GESTÃO | EXISTE · gate `tenant.members.create`/`delete`; token de uso único, auditado, sem escalonamento |

### 2.3 Módulos de domínio (tenant) — permissões CRUD reais

| Módulo | Endpoints | Tela Nexora | Menu (gate) | Status |
|---|---|---|---|---|
| Clientes | `GET/POST/PUT/DELETE /customers` (+ `?search&status&sort&page&pageSize`) | `/clientes` (CustomerPage) | `customers.read` + feat `CUSTOMERS` | EXISTE · **INCOMPLETA** (sem detalhe `GET /{id}`, sem paginação plugada, sem colunas de status/telefone padronizadas) |
| Profissionais | `GET/POST/PUT/DELETE /professionals` + `PUT/DELETE /{id}/services/{sid}` | `/profissionais` (CatalogPage kind) | `professionals.read` + feat | EXISTE · **INCOMPLETA** (vínculo profissional↔serviço sem UI; sem detalhe) |
| Serviços | `GET/POST/PUT/DELETE /services` | `/servicos` (CatalogPage kind) | `services.read` + feat | EXISTE · INCOMPLETA (revisar campos: duração, preço, ativo) |
| Agenda | `GET/POST/PUT /appointments` + `PATCH /{id}/status` `/cancel`; `working-hours` GET/PUT; `blocked-periods` GET/POST/DELETE; `scheduling/context` | `/agenda` (SchedulePage) | `appointments.read` + feat `SCHEDULING` | EXISTE · **INCOMPLETA** (campos UUID em vez de selects; sem visão semana/mês polida) |
| Relatórios (tenant) | `GET /reports/overview?from&to` | `/relatorios` (ReportsPage) | `reports.read` + feat `REPORTS` | EXISTE · INCOMPLETA (alinhar ao dashboard DentalFlow) |

### 2.4 Billing / Assinatura (tenant) — gate `tenant.manage`

| Funcionalidade | Endpoint | Tela Nexora | Menu | Status |
|---|---|---|---|---|
| Ver assinatura | `GET /subscription` | `/assinatura` (BillingPage) | GESTÃO | EXISTE |
| Faturas do tenant | `GET /billing/invoices` | `/assinatura` | GESTÃO | EXISTE / INCOMPLETA |
| Iniciar checkout | `POST /billing/checkout` | `/assinatura` | GESTÃO | EXISTE |

### 2.5 Onboarding / Vertical

| Funcionalidade | Endpoint | Tela | Status |
|---|---|---|---|
| Rascunho de onboarding | `POST/GET/PUT /onboarding/drafts` + `/complete` | `/onboarding` (wizard) | EXISTE |
| Segmentos / Planos (onboarding) | `GET /onboarding/segments` `/plans` | `/onboarding` | EXISTE (vazio sem seed — ver CONTAS-DE-TESTE §4) |
| Config. inicial da vertical | `GET /vertical-setup` `POST /vertical-setup/apply` | `/configuracao-inicial` | EXISTE · SEM MENU (só no fluxo; avaliar link no hub) |

### 2.6 Administração de plataforma — policy `PlatformAdmin`

| Funcionalidade | Endpoint | Tela Nexora | Menu | Status |
|---|---|---|---|---|
| Dashboard da plataforma | `GET /admin/dashboard` | `/admin` seção "Dashboard" | PLATAFORMA | **PADRÃO ANTIGO** (switcher interno cru; não é o padrão Configurações DF) |
| Listar tenants | `GET /admin/tenants` | `/admin` seção "Tenants" | PLATAFORMA | PADRÃO ANTIGO |
| Detalhe do tenant | `GET /admin/tenants/{id}` | — | — | **SEM TELA** |
| Ativar/desativar tenant | `PATCH /admin/tenants/{id}/status` | `/admin` (toggle) | PLATAFORMA | EXISTE (sem confirmação/modal) |
| Overrides de feature por tenant | `GET/PUT /admin/tenants/{id}/feature-overrides` | — | — | **SEM TELA** (service method já existe no front) |
| Listar usuários (plataforma) | `GET /admin/users` | `/admin` seção "Usuários" | PLATAFORMA | **PADRÃO ANTIGO / INCOMPLETA** (lista crua; sem tenant/perfil/último acesso; sem ações — ver §7) |
| Segmentos (CRUD) | `GET/POST/PUT /admin/segments` | `/admin` seção "Segmentos" | PLATAFORMA | INCOMPLETA (cria por `code/name` inline; sem editar/ativar via UI) |
| Auditoria | `GET /admin/audit-logs` | `/admin` seção "Auditoria" | PLATAFORMA | PADRÃO ANTIGO (tabela crua; sem filtro por ator/ação/data) |
| Features (CRUD) | `GET/POST/PUT /admin/features` | `/admin` seção "Features" | PLATAFORMA | INCOMPLETA (cria inline; sem editar/ativar) |
| Planos (CRUD) | `GET/POST/PUT /admin/plans` | `/admin` seção "Planos" | PLATAFORMA | INCOMPLETA (toggle público/trial ok; **configurar plano×feature sem UI** — método existe) |
| Config. plano × feature | `PUT /admin/plans/{planId}/features/{featureId}` | — | — | **SEM TELA** (método `configurePlanFeature` no service, sem UI) |
| Assinaturas (admin) | `GET /admin/subscriptions` (+ `/{id}`, `/{id}/events`) | `/admin/assinaturas` (SubscriptionAdminPage) | PLATAFORMA | EXISTE / INCOMPLETA (campos UUID) |
| Ciclo de vida da assinatura | `POST /admin/subscriptions/{id}/{activate\|change-plan\|cancel-at-period-end\|cancel-immediately\|mark-past-due\|process}` | `/admin/assinaturas` | PLATAFORMA | EXISTE (ações; revisar confirmações) |
| Criar trial | `POST /admin/subscriptions` | `/admin/assinaturas` | PLATAFORMA | EXISTE |
| Preços de plano | `GET/POST /admin/billing/prices` | `/admin/billing` (BillingAdminPage) | PLATAFORMA | EXISTE / INCOMPLETA |
| Faturas / Pagamentos (plataforma) | `GET /admin/billing/invoices` `/payments` | `/admin/billing` | PLATAFORMA | EXISTE / INCOMPLETA |
| Relatórios da plataforma | `GET /admin/reports/overview` | `/admin/relatorios` (ReportsPage platform) | PLATAFORMA | EXISTE / INCOMPLETA |
| Webhook Mercado Pago | `POST /payments/webhooks/mercado-pago` | — | — | **N/A** (técnico) |

---

## 3. Rotas × Menu × Página

| Rota | Componente | No menu | Guarda | Observação |
|---|---|---|---|---|
| `` | HomePage | PRINCIPAL "Visão Geral" | auth | dashboard + seletor de tenant |
| `login` / `cadastro` | AuthPage | — | público | **aprovado** |
| `selecionar-empresa` | TenantSelectPage | — | auth | |
| `onboarding` | OnboardingPage | — | auth | fluxo |
| `configuracao-inicial` | VerticalSetupPage | — | auth+tenant | **SEM MENU** (avaliar no hub) |
| `agenda` | SchedulePage | PRINCIPAL | auth+tenant+feat | |
| `clientes` | CustomerPage | PRINCIPAL | auth+tenant+feat | |
| `profissionais` | CatalogPage | PRINCIPAL | auth+tenant+feat | |
| `servicos` | CatalogPage | PRINCIPAL | auth+tenant+feat | |
| `relatorios` | ReportsPage | PRINCIPAL | auth+tenant+feat | |
| `equipe` | TeamPage | GESTÃO (só `tenant.manage`) | auth+tenant | |
| `assinatura` | BillingPage | GESTÃO (só `tenant.manage`) | auth+tenant | |
| `configuracoes` | SettingsHubPage | GESTÃO | auth+tenant | hub |
| `empresa` | EmpresaPage | ⚠️ só via hub | auth+tenant | |
| `perfis` | RolesPage | ⚠️ só via hub | auth+tenant | |
| `permissoes` | PermissionsPage | ⚠️ só via hub | auth+tenant | |
| `perfil` | ProfilePage | dropdown | auth | |
| `admin` | AdminPage | PLATAFORMA | platformAdmin | |
| `admin/assinaturas` | SubscriptionAdminPage | PLATAFORMA | platformAdmin | |
| `admin/billing` | BillingAdminPage | PLATAFORMA | platformAdmin | |
| `admin/relatorios` | ReportsPage | PLATAFORMA | platformAdmin | |
| `403` / `erro` / `**` | ErrorPage | — | — | |

**Sem rota órfã de menu. Nenhum item de menu aponta para rota inexistente.**
Itens de menu **ausentes** que a auditoria recomenda avaliar: nenhum item novo de topo é
necessário — as telas de administração de empresa continuam no hub `/configuracoes` (intencional).

---

## 4. DentalFlow — telas/padrões de referência

| Padrão DF | Onde no DF | Uso no Nexora |
|---|---|---|
| `main-layout` + `sidebar` + `header` | `layout/` | shell (`shared/ui/shell/`) — **recolorir sidebar p/ claro** |
| `dashboard` — `df-page-header` + grid `df-metric` + 2col `df-card` (barras CSS, listas) | `features/dashboard` | **Fase C** — refazer `/` |
| `configuracoes` — `.df-settings-grid` (nav lateral 220px + conteúdo) + `df-card` por seção + `df-switch` + `df-modal` | `features/configuracoes` | **Fase D** — molde de `/admin` **e** do hub `/configuracoes` |
| `pacientes` (lista) — page-header + `df-list-toolbar` + `df-table` (person + badge + ações-ícone) + empty + pagination + confirm | `features/pacientes` | molde de Clientes/Serviços/Profissionais/Usuários |
| `pacientes/form` — rota própria, `df-form-grid`, seções, `df-form-actions` | `features/pacientes/paciente-form` | forms grandes; forms curtos = `NxModal` |
| `perfil` — card conta + avatar + campos + switches | `features/perfil` | `/perfil` (Fase K) |
| `df-toast-container` + `NotificationService` | `shared/components/toast` | **Nexora não tem toast** — hoje usa `error()`/`saved()` inline. Avaliar `NxToast` (Fase M) |

Componentes DF vazios (só pastas, não implementados): `tabs`, `select`, `input`, `breadcrumb`,
`dropdown`, `icon-button`, `error-state`, `confirm-dialog`, `search-input`. **Não copiar desses.**

`shared/ui/` do Nexora já cobre: PageHeader, Card, Badge, Button, StatCard, DataTable, EmptyState,
Pagination, Modal, ConfirmDialog, SearchInput, ChipFilter, Switch, Avatar, FormField, Sidebar, Topbar.
**Faltam** (candidatos): `NxToast`/serviço de notificação, `NxTabs`/`NxPageTabs`, `NxPermissionMatrix`,
`NxSettingsLayout` (nav lateral + conteúdo), `NxSelect` (hoje `<select>` cru em FormField).

---

## 5. Checklist de cobertura (§27 da tarefa)

### Telas
- [x] Login / Cadastro — **aprovado**
- [ ] Dashboard (Fase C — hoje não equivale ao DF)
- [ ] Administração / workspace no padrão Configurações DF (Fase D)
- [ ] Empresas / Tenants — detalhe + feature-overrides + modais (Fase E)
- [ ] Usuários (plataforma) — tela completa (Fase F)
- [ ] Usuários / membros do tenant — refino (Fase F)
- [ ] Perfis e Permissões — matriz perfil × módulo × ação (Fase G)
- [ ] Serviços — CRUD refinado (Fase H)
- [ ] Profissionais — CRUD + vínculo de serviços (Fase I)
- [ ] Clientes — detalhe + paginação + colunas padrão (Fase J)
- [ ] Configurações do tenant — hub no padrão DF (Fase K)
- [ ] Segmentos / Features / Planos / Auditoria — telas de admin (Fase L)
- [ ] Modais / mensagens / toast / empty / loading unificados (Fase M)
- [ ] Responsividade validada em navegador real (Fase N)
- [x] **Tema — somente claro, alternador removido (2026-09-02)**
- [ ] Sidebar clara (Fase B)

### Achados estruturais
- **BACKEND SEM UI:** `GET /admin/tenants/{id}`, `GET/PUT /admin/tenants/{id}/feature-overrides`,
  `PUT /admin/plans/{planId}/features/{featureId}`, `GET /customers/{id}`, `GET /professionals/{id}`,
  `PUT/DELETE /professionals/{id}/services/{serviceId}`, `GET /subscriptions/{id}/events`,
  `GET /t/{slug}` (página pública).
- **UI SEM ENDPOINT:** nenhum (o convite de usuário no tenant passou a ter backend + UI em 2026-09-03).
- **ROTAS SEM MENU:** `configuracao-inicial` (intencional — fluxo); `empresa`/`perfis`/`permissoes`
  (intencional — via hub).
- **MENU SEM ROTA:** nenhum.
- **PERMISSÕES SEM REPRESENTAÇÃO VISUAL:** `appointments.cancel` e `appointments.update` aparecem no
  catálogo mas a UI de permissões ainda não rotula "Cancelar" com destaque; permissões de plataforma
  (`platform.*`) não têm nenhuma tela (são concedidas em bloco pelo bootstrapper).

---

## 6. Ordem de execução (fases da tarefa)

| Fase | Escopo | Estado |
|---|---|---|
| **A** | Inventário (este documento) | ✅ 2026-09-02 |
| **B** | Design system: **sidebar clara** + consolidar tema claro único | 🟡 tema removido; sidebar pendente |
| **C** | Dashboard `/` no padrão DF | ⬜ |
| **D** | `/admin` workspace no padrão "Configurações" DF (`NxSettingsLayout`) | ⬜ |
| **E** | Empresas/Tenants — detalhe, feature-overrides, modais, confirmações | ⬜ |
| **F** | Usuários (plataforma) + refino de membros do tenant | PARCIAL — convite de membro do tenant (modal + pendentes + `/convite/aceitar`) feito 2026-09-03; usuários da plataforma ⬜ |
| **G** | Perfis e Permissões — `NxPermissionMatrix` (perfil × módulo × ação) | ⬜ (ver §7) |
| **H** | Serviços — CRUD refinado (duração, preço, ativo) | ⬜ |
| **I** | Profissionais — CRUD + vínculo de serviços | ⬜ |
| **J** | Clientes — detalhe, paginação, colunas | ⬜ |
| **K** | Configurações do tenant — hub no padrão DF | ⬜ |
| **L** | Segmentos / Features / Planos / Auditoria | ⬜ |
| **M** | Modais / mensagens / `NxToast` / empty / loading | ⬜ |
| **N** | Responsividade (1920/1366/1024/768/390/360) em navegador real | ⬜ |
| **O** | Auditoria final de rotas/menu/permissões | ⬜ |
| — | Usuários de teste (§11) — estender `scripts/seed-contas-teste.ps1` | ⬜ |

---

## 7. Pontos que exigem decisão de arquitetura (§24 — PARAR)

A tarefa pede, para cada módulo, permissões **Consultar / Incluir / Editar / Excluir**. Situação real:

| Área | Granularidade hoje | Pedido | Ação |
|---|---|---|---|
| `customers` / `professionals` / `services` | **CRUD completo** já existe | CRUD | ✅ construir a UI direto sobre `TenantPermissions.Catalog` |
| `appointments` | `read/create/update/**cancel**` (sem `delete`) | CRUD | ✅ mapear "Excluir" → ausente; expor "Cancelar" |
| `reports` | só `read` | Consultar | ✅ só checkbox "Consultar" |
| **Gestão de membros** (`/equipe`) | **`tenant.members.{read,create,update,delete}`** — CRUD real (2026-09-03) | CRUD por ação | ✅ resolvido. A matriz representa cada uma das quatro chaves; o convite de usuário tem UI. |
| **Administração da empresa** (`/empresa`, `/perfis`, `/permissoes`, billing) | **gate único `tenant.manage`** (perfil / papéis / billing) | CRUD por sub-área | ⚠️ **GAP parcial**. `members.*` já saiu do `tenant.manage`; falta split de `tenant.profile.*` / `tenant.roles.*` / `tenant.billing.*`. **Decisão do Kelson necessária antes de mexer nesses.** |
| **Plataforma** (`/admin/*`) | papel `PlatformAdmin` tudo-ou-nada | — | ⚠️ sem permissões finas. A UI de administração continua gated só pelo papel. Sem ação até haver requisito. |

**Fases que NÃO dependem desses gaps podem prosseguir:** B, C, D (visual), E, H, I, J, K, L, M, N.
**Fase G** (matriz de permissões) prossegue para os módulos de domínio e representa `tenant.manage`
como item único até a decisão acima.

---

## 8. Log

| Data | Fase | O que foi feito |
|---|---|---|
| 2026-09-02 | A | Inventário completo: 27 arquivos de endpoint mapeados, catálogo de permissões, rotas × menu, referência DentalFlow, gaps. Documento criado. |
| 2026-09-02 | B (parcial) | Alternador de tema removido (app.ts / nx-topbar / styles.scss / app.config.ts): sem dark mode, sem `prefers-color-scheme`, sem `localStorage` de tema, `darkModeSelector:false` no PrimeNG. Testes: `+7` (app.spec, nx-topbar.spec). Build verde, 36/36. |
| 2026-09-03 | F (parcial) | Convite de membro do tenant: backend `tenant.members.*` CRUD + workflow `TenantInvitation` (create/list/resend/cancel/accept anônimo) com token de uso único, não escalonamento e auditoria — **61 testes de integração** (49 + 12 de gap). `RoleGrant` (regra `RolePermissions ⊆ ActorPermissions`) passou a proteger também `SetRolePermissionsAsync` — `+5` testes (`RolePermissionEscalationTests`). Frontend: `NxToast`, TeamPage com "Incluir usuário" (modal), convites pendentes, reenviar/cancelar, gating por `tenant.members.*`; `/convite/aceitar` público. Nav: item "Usuários e Equipe" passa a gatear em `tenant.members.read`. `appsettings.json` ganhou `Tenancy:Invitations:ExpirationDays: 7`. Build front verde, 38 testes. |
