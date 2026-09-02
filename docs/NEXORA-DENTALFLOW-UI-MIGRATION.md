# Nexora ← DentalFlow — Migração de Layout Visual

**Objetivo:** o frontend do Nexora deve usar o DentalFlow (`C:\Users\Kelso\source\repos\dentalflow\frontend`)
como **template visual oficial** — sidebar, header, cards, botões, tabelas, formulários, modais,
responsividade idênticos. Mudam apenas marca, textos, menus, campos, entidades e funcionalidades.

**Não muda:** arquitetura funcional, rotas, guards, interceptors, `AuthService`, RBAC, multi-tenancy,
`TenantId`, PlatformAdmin, contratos de API, billing. Esta tarefa é **visual**.

Status: 🟢 Fases 1–7 concluídas (Fase 6 = auditoria de CSS; validação ao vivo <1920 bloqueada por tooling).

**Decisões tomadas (2026-09-01):** D1 = **sidebar NAVY** (`#171b2e`, igual DentalFlow) — reverte a
escolha anterior de sidebar clara. D2 = **biblioteca `shared/ui` completa** + refatorar todas as telas.

---

## ~~Débito — camada de normalização `.nx-content`~~ — RESOLVIDO (Fase 7)

O bloco `.nx-content article/form/button/input {…!important}` foi **removido inteiro** na Fase 7.
Restou só `.nx-content>main` (reset dos 2 fluxos de setup), `.nx-error` e `[hidden]{display:none!important}`.
`styles.scss` caiu de ~47 kB para ~43 kB. Telas agora usam exclusivamente classes/componentes do DS.

## Fase 1 — Inventário

### 1.1 DentalFlow — Design System (fonte da verdade)

`src/styles.scss` → `@import` de `assets/styles/_tokens.scss`, `_base.scss`, `_data-views.scss`;
mixins em `_breakpoints.scss` e `_form-mixins.scss` (resolvidos via `stylePreprocessorOptions.includePaths`).

**Tokens (`_tokens.scss`, prefixo `--df-`):**

| Grupo | Valores |
|---|---|
| Primária | `#6c5ce7` / hover `#5a4bd1` / light `#ede9fe` / lighter `#f5f3ff` |
| Sidebar | bg `#171b2e` · bg-alt `#1d2238` · text `#9ca3c4` · text-active `#fff` · active-bg `#6c5ce7` |
| Neutros | bg `#f4f5fa` · surface `#fff` · border `#e7e9f2` · text `#1f2333` · text-muted `#8a8fa3` · text-light `#b0b4c6` |
| Semânticas | success `#22c55e`/bg `#e9fbf0` · danger `#ef4444`/bg `#fdecec` · warning `#f59e0b`/bg `#fef6e7` · info `#3b82f6`/bg `#eaf2fe` |
| Tipografia | Inter; `--df-fs-xs .75 / sm .8125 / base .9375 / md 1 / lg 1.25 / xl 1.5 / xxl 1.875` rem; pesos 400/500/600/700 |
| Espaço | `--df-space-1..7` = 4 / 8 / 12 / 16 / 24 / 32 / 40 px |
| Raio | sm 8 · md 12 · lg 16 · pill 999 |
| Sombra | sm `0 1px 2px rgba(23,27,46,.06)` · md `0 4px 16px …/.06` · lg `0 10px 30px …/.1` |
| Layout | sidebar 250px · colapsada 76px · header 72px · transição `.2s ease` |
| Breakpoints | sm 480 · md 768 · lg 1024 · xl 1280 · xxl 1440 (mixins `df-up`/`df-down`) |

**Padrões globais compartilhados:**
- `_base.scss` — reset, foco visível (`outline 2px primary`), `.df-sr-only`, scrollbar.
- `_data-views.scss` — `.df-list-toolbar`, `.df-search`, `.df-status-filter` (pílulas), `.df-table-wrapper`,
  `.df-table` (+ `__actions` botões-ícone 36px, transformação **thead some / linha vira card** em `<md`),
  `.df-table-person`, `.df-table-skeleton`, `.df-confirm-overlay` + `.df-confirm`.
- `_form-mixins.scss` — mixins `df-field` (label + input/select/textarea + `__error`/`__hint`, `--error`, `--full`),
  `df-form-grid` (1col → 2col em md), `df-field-row` (2 lado a lado, colapsa em sm), `df-metric` (ícone 44px + label + valor).

### 1.2 DentalFlow — Componentes compartilhados (`src/app/shared/components/`)

| Componente | Selector | Notas para o Nexora |
|---|---|---|
| Button | `df-button` | variants `primary/secondary/ghost/danger`, sizes `sm/md/lg`, `icon`, `loading` (spinner), `disabled` |
| Card | `df-card` | surface + border + shadow-sm + padding-5; `noPadding` |
| Badge | `df-badge` | tones `success/danger/warning/info/neutral` (cor + bg suave, pill, 600) |
| PageHeader | `df-page-header` | `title` + `subtitle` + slot de ações (`<ng-content>`) |
| EmptyState | `df-empty-state` | `icon` + `title` + `description` + slot (CTA) |
| Skeleton | `df-skeleton` | `width`/`height`, shimmer |
| Pagination | `df-pagination` | `paginaAtual`/`tamanhoPagina`/`total` → `(paginaChange)`; "Mostrando x–y de z" + ‹ n/N › |
| Modal | `df-modal` | **focus-trap + ESC + devolve foco**; `title` + `(closed)`; header/body; `max-width 560px`, `90dvh` |
| Avatar | `df-avatar` | `src`/`alt`/`size`; fallback com iniciais |
| Switch | `df-switch` | `checked`/`disabled`/`label` → `(checkedChange)` |
| Toast | `df-toast-container` | fixo top-right, tipos success/error/warning/info; ligado ao `NotificationService` |
| (vazios) | tabs, search-input, confirm-dialog, select, input, breadcrumb, dropdown, icon-button, error-state | **não implementados no DentalFlow** — apenas pastas |

### 1.3 DentalFlow — Shell (`src/app/layout/`)

- `main-layout` — `.df-shell` flex, `height 100dvh`, `overflow hidden`; sidebar fixa (desktop) /
  drawer + overlay (`<992px`); `HostListener resize`; título da rota via `data.titulo`; `.df-shell__content` `padding-5` (`-4` mobile).
- `sidebar` — `.df-sidebar` navy, `width 250/76`; `__brand` (ícone quadrado primário + nome + subtítulo),
  `__user` (avatar + nome + papel, bloco `bg-alt`), `__nav` (lista filtrada por `AutorizacaoService.podeLer(modulo)`),
  `__footer` (Ajuda + Sair). Item: `10px 12px`, radius-sm; hover `bg-alt`; ativo `active-bg` (roxo).
- `header` — `.df-header` surface, altura 72; botão hambúrguer (`≤`), título da rota, busca global (só `≥768`),
  sino de notificações (com dot), avatar → dropdown (`Meu perfil` / `Configurações` / `Sair`).

### 1.4 DentalFlow — Telas de referência (padrões a reaproveitar)

| Tela | Padrão |
|---|---|
| `auth/login` | card centrado sobre fundo **navy** (`--df-sidebar-bg`), brand em cima, `.df-field` próprio (menor), botão `lg` full |
| `dashboard` | `df-page-header` + grid `df-metric` (auto-fit minmax 220) + grid 2col de `df-card` (barras CSS, listas `.df-simple-list`) |
| `pacientes` (lista) | `df-page-header` + ação `df-button icon=plus` · `.df-list-toolbar` (`.df-search` + `.df-status-filter`) · `.df-table-wrapper` > `.df-table` (person + badge + `__actions` ícones) · `df-empty-state` · `df-pagination` · `.df-confirm-overlay` |
| `pacientes/form` | rota própria; `.df-form-grid`, `.df-field`, seções, `.df-form-actions` (df-button submit + cancelar) |
| `configuracoes` | `.df-settings-grid` (nav lateral 220px + conteúdo) · `df-card` por seção · `df-switch` em `.df-pref-row` · `df-modal` para novo/editar usuário |
| `perfil` | card conta + avatar + `df-field` + `df-switch` de preferências |

### 1.5 Nexora — Estado atual do frontend

`frontend/nexora-web` — Angular 22 + PrimeNG 22. DS já em `src/styles.scss` `@layer nexora` com tokens
`--nx-*` **visualmente quase idênticos** ao DentalFlow (primária `#6a5cf0` ≈ `#6c5ce7`, mesmas escalas de
espaço/raio/sombra, mesma transformação tabela→card em `<768`, drawer `<992`). Diferenças-chave:

- **Sidebar clara** (decisão anterior) vs navy do DentalFlow → **D1**.
- **Sem componentes** `shared/ui` reais (só `nx-button` wrapper de `p-button`, não usado) → **D2**.
- Ícones: **sprite SVG inline** no `app-root` (privacidade/CSP/sem dep) vs FontAwesome do DentalFlow →
  manter sprite, mapear ícones.
- Páginas: `foo-page.html` + `foo-page.scss` (sem infixo `.component`).
- Já migrados nesta leva (sessão 2026-09-01 noite): toolbar+chip-filter+modal+confirm em
  customer/catalog/team/roles; login/cadastro 2 painéis; dashboard hero; menu em 3 grupos.

### 1.6 Nexora — Rotas × Menu (cobertura)

| Rota | Componente | No menu? | Observação |
|---|---|---|---|
| `''` | HomePage (Visão Geral) | ✅ PRINCIPAL | dashboard + seletor de tenant |
| `login` / `cadastro` | AuthPage | — | público |
| `onboarding` | OnboardingPage | — | pós-cadastro (fluxo) |
| `configuracao-inicial` | VerticalSetupPage | ❌ | pós-onboarding; **avaliar** link no hub Configurações |
| `clientes` | CustomerPage | ✅ (customers.read) | |
| `profissionais` | CatalogPage(kind) | ✅ (professionals.read) | |
| `servicos` | CatalogPage(kind) | ✅ (services.read) | |
| `agenda` | SchedulePage | ✅ (appointments.read) | |
| `relatorios` | ReportsPage | ✅ (reports.read) | |
| `equipe` | TeamPage | ✅ GESTÃO | |
| `assinatura` | BillingPage | ✅ GESTÃO | |
| `configuracoes` | SettingsHubPage | ✅ GESTÃO | hub |
| `empresa` | EmpresaPage | ⚠️ só via hub | intencional (menu enxuto) |
| `perfis` | RolesPage | ⚠️ só via hub | intencional |
| `permissoes` | PermissionsPage | ⚠️ só via hub | intencional |
| `perfil` | ProfilePage | dropdown | |
| `admin` | AdminPage | ✅ PLATAFORMA (platformAdmin) | |
| `admin/assinaturas` | SubscriptionAdminPage | ✅ PLATAFORMA | |
| `admin/billing` | BillingAdminPage | ✅ PLATAFORMA | |
| `admin/relatorios` | ReportsPage (platform) | ✅ PLATAFORMA | |
| `403` / `erro` / `**` | ErrorPage | — | |

Sem rota órfã de menu detectada (fora as intencionais acima). Nenhuma entrada de menu aponta para rota inexistente.

---

## Fases

- [x] **Fase 1 — Inventário** (este documento)
- [x] **Fase 2 — Design System** — tokens `--nx-*` realinhados a `--df-*` (primária `#6c5ce7`, sidebar navy, semânticas, bg `#f4f5fa`, mono, font-weights); dark mode ganhou tokens de sidebar
- [x] **Fase 3 — Shell** — sidebar navy + rodapé (Meu perfil / Sair); topbar com botões-ícone quadrados (padrão DentalFlow); `NxSidebar` + `NxTopbar` extraídos para `shared/ui/shell/`; `app.ts` monta `navGroups` e passa por input
- [x] **Fase 4 — Componentes compartilhados** (`src/app/shared/ui/`): `NxPageHeader`, `NxCard`,
  `NxBadge`, `NxButton` (selector `button[nx-button], a[nx-button]`), `NxStatCard`, `NxDataTable`,
  `NxEmptyState`, `NxPagination`, `NxModal` (focus-trap + ESC), `NxConfirmDialog`, `NxSearchInput`
  (debounce), `NxChipFilter`, `NxSwitch`, `NxAvatar`, `NxFormField`. Barril em `shared/ui/index.ts`.
  Removido o `nx-button` antigo (wrapper `p-button` não usado).
- [x] **Fase 5 — Telas**: customer, catalog (prof./serviços), team, roles, home (dashboard), profile,
  empresa, permissions, billing, reports, schedule (agenda — 3 forms → modais), settings-hub, admin
  (3 páginas), onboarding, vertical-setup. `<main>` removido de todas menos onboarding/vertical-setup.
- [x] **Fase 6 — Responsividade** — auditoria das media queries + 6 correções (hosts `display:block`,
  ritmo vertical, `.nx-modal-footer` mobile, forms de admin em coluna, ações do page-header em ≤480).
  Ao vivo só 1920 (tooling não redimensiona).
- [x] **Fase 7 — remover normalização `.nx-content`**: bloco inteiro removido (`styles.scss` -4 kB);
  sobrou só `.nx-content>main` reset + `.nx-error` + `[hidden]{display:none!important}`. `!important`
  temporários das primitivas também removidos.

### Checklist de cobertura

- [x] Shell · [x] Sidebar (navy) · [x] Header
- [x] Dashboard — `NxStatCard` (KPI tone) + `NxCard` + hero
- [x] Listagens — `NxDataTable` + `NxSearchInput` + `NxChipFilter` + `NxEmptyState` em Clientes,
  Profissionais, Serviços, Equipe, Papéis, Relatórios, Billing (tenant+admin), Admin. `NxPagination` disponível (ainda não plugado em lista paginada).
- [x] Formulários — `NxFormField` + `NxButton`; modais de Clientes/Prof./Serv./Papéis/Agenda; Empresa, Permissões, Billing, Reports refeitos
- [x] Modais — `NxModal` (focus-trap + ESC) + `NxConfirmDialog`
- [x] Login / Cadastro (2 painéis)
- [x] Perfil — `NxCard` + `NxAvatar` + `NxBadge`
- [x] Configurações (hub agrupado) + Empresa + Perfis + Permissões
- [x] Responsividade — media queries auditadas (1200/992/768/480, espelham `responsive.css` do DentalFlow) + 6 correções; validação ao vivo só em 1920 (tooling não redimensiona a janela)
- [x] Build (verde; budget inicial estourado ~219 kB = débito conhecido, regra 53)
- [x] Testes (`ng test` 15/15)

---

## Log de progresso

| Data | Fase | O que foi feito |
|---|---|---|
| 2026-09-01 | 1 | Inventário completo de DentalFlow (DS, componentes, shell, telas) e Nexora (rotas × menu, estado do DS). Documento criado. |
| 2026-09-01 | 2 | Tokens `--nx-*` realinhados a `--df-*`. Sidebar navy (`--nx-sidebar-*`), primária `#6c5ce7`, bg `#f4f5fa`, semânticas exatas, `--nx-font-mono`, `--nx-fw-*`. Dark mode ganhou tokens de sidebar. |
| 2026-09-01 | 3 | Sidebar reescrita para navy (bg-alt no bloco de usuário, grupos, rodapé Meu perfil/Sair, item ativo roxo sem barra lateral). Topbar: botões-ícone quadrados bordados (36px), título `fs-lg`. Componentes `NxSidebar`/`NxTopbar` em `shared/ui/shell/` (+ tipos `nav.ts`); `app.ts` reescrito (removido `userMenuOpen`, adicionados `shellUser`/`shellPlan`/`navGroups` computed); `app.html` shell agora usa `<nx-sidebar>`/`<nx-topbar>`. QA ao vivo: cadastro→onboarding→shell OK, navy sobre canvas claro = visual DentalFlow; dashboard e Clientes renderizam. Achado: normalização `.nx-content` conflita com primitivas novas (corrigido com `!important` temporário; remoção completa na Fase 7). `/clientes` às vezes redireciona p/ `/` — comportamento de auth pré-existente do Nexora (token de refresh sem escopo de tenant), não causado pela migração. `ng build` verde, `ng test` 15/15. |
| 2026-09-02 | 4 | 15 componentes `src/app/shared/ui/` criados + barril (`index.ts`). `NxButton` como selector de atributo (`button[nx-button], a[nx-button]`) para funcionar em `<button>` e `<a routerLink>`. `NxModal` com focus-trap/ESC/restauração de foco. `nx-button` antigo (p-button, sem uso) removido. CSS novo: `.nx-btn-spinner`, `.nx-table-skeleton`, `.nx-pagination` refeito (space-between + `.nx-pg-controls`/`.nx-pg-current`). |
| 2026-09-02 | 5 | 16 telas refatoradas: customer, catalog (prof./serviços), team, roles, home (dashboard), profile, empresa, permissions, billing, reports, schedule (3 forms inline → 3 `NxModal`), settings-hub, admin (×3), onboarding, vertical-setup. `<main>` removido de todas menos onboarding/vertical-setup. `initials()` locais removidos (→ `NxAvatar`). `badge()` passou a devolver o tom (`'success'`…) em vez da classe. |
| 2026-09-02 | 7 | Bloco `.nx-content` normalization removido inteiro (`styles.scss` 47→43 kB). Restou `.nx-content>main` reset + `.nx-error` + `[hidden]{display:none!important}` (corrige o `<button hidden>` de submit dos modais). `!important` temporários das primitivas removidos. QA ao vivo (1920, `ui-check-b7@nexora.local`): dashboard (KPI tone), Serviços (empty + modal sem artefato), Agenda, Configurações (hub), Empresa (form 2col) em **dark mode** — consistente com DentalFlow. `hScroll:false` em 1920. `ng build` verde, `ng test` 15/15. |

## Pendências reais

1. **Fase 6** — validar 1366/1024/768/390/360 ao vivo (o navegador de automação trava o viewport; media queries do `styles.scss` não foram tocadas e já cobrem 1200/992/768/480).
2. **Budget do Angular** — bundle inicial ~219 kB acima (era ~96 kB antes de todo o trabalho visual). Dívida documentada (regra 53: não subir o budget). Candidato a `defer`/split se virar bloqueio.
3. **`NxPagination`** existe mas nenhuma listagem do Nexora é paginada de fato (backend devolve tudo); plugar quando houver.
4. **`/clientes` → `/`** intermitente logo após login — token de refresh sem escopo de tenant. É comportamento de auth pré-existente do Nexora, **fora do escopo visual**; não regrida por causa disso.
5. `schedule` e `admin` ainda usam campos de **UUID** em vez de selects (limitação de endpoint, não visual).

## Fase 6 — Responsividade

**Limitação de tooling:** o navegador de automação desta sessão **não redimensiona** (`resize_window`
retorna sucesso mas `window.innerWidth` fica travado em 1920; confirmado 2×). Igual à nota do cofre.
Validação feita por **auditoria das media queries** de `styles.scss` (breakpoints 1200/992/768/480,
espelham o `responsive.css` do DentalFlow) + verificação ao vivo só em 1920 (sem scroll horizontal).

### Comportamento por breakpoint (auditado)

| Faixa | Shell | Grids | Listas / tabelas | Modais |
|---|---|---|---|---|
| ≥1200 (1920, 1366) | sidebar fixa 250px | KPI 4 col, `nx-two-col` 1.1/1fr, `nx-grid--2` 2col | tabela normal (`min-width:640` rola dentro do card se faltar espaço) | centrado, `max-width` 400/480 |
| 992–1199 (1024) | sidebar fixa 250px | KPI 2col, `nx-two-col` 1col, `nx-grid--2` 2col | idem | idem |
| 768–991 | **drawer + scrim + hambúrguer**; topbar sem busca; user-chip só avatar | KPI 2col; `nx-form-row` 1col; `nx-settings-grid` 1col | idem | `max-width:100%` |
| ≤768 (768×1024) | drawer | KPI `1fr 1fr` → `.nx-kpi` empilha ícone; `nx-two-col` 1col; `nx-list-toolbar` coluna | **tabela vira cartão** (`thead` some, `td` flex + `::before` label) | full-width; forms de admin/sub/preço/relatório viram coluna |
| ≤480 (390×844, 360×800) | drawer | KPI 1col | cartão | overlay `space-3`; header/body/footer `space-4`; **footer `column-reverse`, botões 100%**; `nx-page-header` coluna, ações 100% |

### Correções aplicadas nesta fase
1. `display:block` nos hosts `nx-page-header / nx-card / nx-data-table / nx-empty-state / nx-form-field / nx-search-input / nx-pagination / nx-confirm-dialog` (eram custom elements `display:inline` — só funcionavam por blockificação de grid/flex).
2. Ritmo vertical: `.nx-content>*+*{margin-top:var(--nx-space-5)}` (antes dependia do `margin-bottom` do `.nx-page-header`, que foi zerado); `.nx-alert` perdeu `margin-bottom`.
3. `.nx-modal-footer` ganhou `flex-wrap:wrap`; em ≤480 vira `column-reverse` com botões 100% (labels longas: "Salvar disponibilidade", "Criar bloqueio").
4. Forms horizontais do Admin (`.nx-admin-create/.nx-sub-form/.nx-price-form/.nx-report-filter`) → coluna em ≤768.
5. `.nx-page-header` ações 100% em ≤480 (`.nx-ph-actions .nx-btn{flex:1}`).
6. Removidos seletores mortos `.nx-content .cards` / `app-admin-page .cards` / `.nx-content form` das media queries.

**Recomendação:** conferir em navegador real (DevTools responsive ou device) — as media queries não
puderam ser exercidas ao vivo aqui.

## Relatório final

1. **Telas encontradas:** 22 rotas (20 de UI + 403/erro/404).
2. **Telas migradas:** 18 + shell (todas as de UI; error-page já usava DS).
3. **Componentes criados:** 17 em `src/app/shared/ui/` (15 + NxSidebar/NxTopbar).
4. **Componentes reutilizados:** classes DS de `styles.scss @layer nexora` (`.nx-table`, `.nx-tabs`, `.nx-alert`, `.nx-switch`, `.nx-agenda-*`, `.nx-kpi--tone`…); sprite SVG de ícones.
5. **Arquivos principais:** `styles.scss` (tokens navy + shell + remoção da normalização), `app.ts`/`app.html` (shell por componente), `shared/ui/*` (novos), 43 templates/ts de telas.
6. **Rotas verificadas:** todas; sem rota órfã (Empresa/Perfis/Permissões via hub — intencional).
7. **Menus verificados:** PRINCIPAL / GESTÃO / PLATAFORMA, gating por permissão preservado; nada de odontologia.
8. **Testes:** `ng test` 15/15.
9. **Build:** verde (budget inicial ~219 kB acima = dívida).
10. **Pendências:** Fase 6 ao vivo (tooling); `NxPagination` não plugado (backend não pagina); UUID em Agenda/Admin (endpoint); `/clientes`→`/` intermitente (auth pré-existente).
11. **Evidências:** screenshots ao vivo em 1920 (login, cadastro, dashboard, Serviços + modal, Agenda, Configurações, Empresa dark mode) — todas consistentes com o DentalFlow.
