# Contas de teste — Nexora

Guia para exercitar **todas as telas e todos os perfis** da aplicação em ambiente local.

> ⚠️ **O Nexora não tem seed de usuários.** Nenhuma conta existe até você criá-la.
> Toda conta nasce por auto‑cadastro (`/cadastro`). Este documento define um conjunto
> padrão de contas, o que cada uma serve para testar, e como provisioná‑las
> (passo a passo pela UI ou pelo script do Apêndice A).
>
> As credenciais abaixo **não são segredos** — são dados descartáveis de ambiente de
> desenvolvimento. Nunca use esses e‑mails/senhas em staging ou produção.

---

## 1. Ambiente local

| Serviço | URL | Observação |
|---|---|---|
| Frontend (Angular) | `http://localhost:4200` | `ng serve` |
| API (.NET) | `http://localhost:8080` | perfil `http` do `launchSettings`; base da API: `http://localhost:8080/api/v1` |
| PostgreSQL | `localhost:5432` | `docker compose up -d postgres` (senha no `.env`) |

O frontend de desenvolvimento aponta **fixo** para `http://localhost:8080/api/v1`
(`src/environments/environment.development.ts`). Suba a API nessa porta.

```bash
# 1. banco
docker compose up -d postgres

# 2. API (aplica migrations automaticamente em Development)
cd backend/src/Nexora.Api && dotnet run

# 3. frontend
cd frontend/nexora-web && npm start
```

### Regras de senha

- entre **12 e 128 caracteres**, sem exigência de complexidade;
- e‑mail precisa conter `@` e ter até 320 caracteres.

**Senha padrão usada em todas as contas deste guia:** `Nexora-Teste-2026`

---

## 2. Tabela mestra de contas

| # | E‑mail | Senha | Perfil | Empresa (slug) | Serve para testar |
|---|--------|-------|--------|----------------|-------------------|
| — | *(sem login)* | — | **Visitante / anônimo** | — | `/login`, `/cadastro`, página pública `/t/{slug}` |
| 1 | `novo.usuario@nexora.test` | `Nexora-Teste-2026` | **Usuário sem empresa** | nenhuma | `/` (sem tenant), `/onboarding`, `/selecionar-empresa`, `/perfil` |
| 2 | `multi.empresa@nexora.test` | `Nexora-Teste-2026` | **Usuário com várias empresas** | `alpha-co`, `bravo-co` | seletor `/selecionar-empresa`, troca de empresa |
| 3 | `platform.admin@nexora.test` | `Nexora-Teste-2026` | **Platform Admin** | nenhuma (escopo plataforma) | `/admin`, `/admin/assinaturas`, `/admin/billing`, `/admin/relatorios` |
| 4 | `admin.barbearia@nexora.test` | `Nexora-Teste-2026` | **Tenant · ADMIN** (papel de sistema) | `barbearia-modelo` | **todas** as telas de tenant |
| 5 | `recepcao@nexora.test` | `Nexora-Teste-2026` | **Tenant · Recepção** (papel custom) | `salao-recepcao` | agenda + clientes, sem configurações/relatórios |
| 6 | `operacao@nexora.test` | `Nexora-Teste-2026` | **Tenant · Operação/Profissional** (papel custom) | `estudio-operacao` | agenda somente leitura/atualização |
| 7 | `financeiro@nexora.test` | `Nexora-Teste-2026` | **Tenant · Financeiro/Relatórios** (papel custom) | `clinica-financeiro` | `/relatorios`, clientes (ver) |
| 8 | `gerente@nexora.test` | `Nexora-Teste-2026` | **Tenant · Gerente** (papel custom, tudo menos `tenant.manage`) | `academia-gerencia` | operação completa sem administração de empresa/papéis |

**Essenciais** para cobrir a aplicação inteira: **#1, #3, #4**.
As contas **#5–#8** só são necessárias para validar telas sob papéis com permissões reduzidas.

> **Por que uma empresa por papel?** O Nexora agora **tem convite de usuário dentro da
> empresa** (tela de Equipe → "Incluir usuário" + `/convite/aceitar`), mas quem convida só
> pode conceder um papel **cujas permissões são um subconjunto das suas** (regra de não
> escalonamento). Para testar um papel não‑ADMIN de forma isolada continua sendo mais
> simples cada papel ter a sua própria empresa/dono descartável — assim você não precisa
> de um segundo ADMIN para promover/rebaixar ninguém.

---

## 3. Perfis em detalhe

### 3.1 Visitante (não autenticado)

| Telas | Comportamento |
|---|---|
| `/login`, `/cadastro` | acesso livre |
| `/t/{slug}` (API pública) | resolve nome/slug/timezone de tenant **ativo**; inexistente/inativo → 404 |
| qualquer rota protegida | `authGuard` → tenta `refresh` → sem sessão → redireciona para `/login` |

### 3.2 Usuário sem empresa (#1)

Recém‑cadastrado, **0 vínculos**. Token **sem** `tenant_id`.

| Tela | O que ver |
|---|---|
| `/` | card "Abrir uma empresa" → como não há empresas, mostra "Criar empresa" / "Sair" |
| `/onboarding` | wizard: Conta → Empresa → Segmento → Plano → conclusão (ver §4 sobre segmentos/planos) |
| `/selecionar-empresa` | com 0 empresas, redireciona para `/` |
| `/perfil` | dados de acesso (somente leitura) |
| `/clientes`, `/agenda`, `/admin`… | `tenantGuard`/`platformAdminGuard` bloqueiam → `/` ou `/403` |

### 3.3 Usuário com várias empresas (#2)

Mesmo usuário dono de `alpha-co` **e** `bravo-co`.

| Fluxo | Esperado |
|---|---|
| login | `GET /api/v1/me/tenants` retorna 2 → navega para `/selecionar-empresa` |
| `/selecionar-empresa` | lista as duas empresas com papel; "Entrar" → `POST /t/{slug}/session` → `/` |
| `/` (sem ter escolhido) | lista as empresas em vez de pedir slug |
| trocar de empresa | logout + login e escolher a outra (a sessão de tenant é reemitida server‑side) |

### 3.4 Platform Admin (#3)

Papel **global** `PlatformAdmin`. Permissões:
`platform.access`, `platform.tenants.manage`, `platform.users.read`,
`platform.segments.manage`, `platform.audit.read`.
Policy `PlatformAdmin` exige `platform.access` **e ausência de** `tenant_id` no token —
Platform Admin **nunca** opera dentro de um tenant.

| Tela | Endpoint | O que testar |
|---|---|---|
| `/admin` | `/api/v1/admin/dashboard`, `/tenants`, `/users`, `/segments`, `/features`, `/plans`, `/audit-logs` | dashboard, ativar/desativar tenant, criar segmento, criar feature, criar plano, configurar `plan/feature`, ver auditoria |
| `/admin/assinaturas` | `/api/v1/admin/subscriptions` + ações `/{id}/{ação}` | ciclo de vida da assinatura (trial/active/pastdue/cancel/expire) |
| `/admin/billing` | `/api/v1/admin/billing/prices\|invoices\|payments` | criar preço de plano, listar faturas e pagamentos |
| `/admin/relatorios` | `/api/v1/admin/reports/overview` | métricas da plataforma |

> **Provisionamento:** ver **Apêndice B** — não há endpoint público de promoção;
> é preciso configurar `Administration:BootstrapAdminEmail` e **reiniciar a API**.

### 3.5 Tenant · ADMIN (#4) — papel de sistema

Criado automaticamente junto com a empresa. Nome do papel: `ADMIN` (`IsSystem = true`,
não editável). Carrega **todas** as `TenantPermissions`:

```
customers.read/create/update/delete
professionals.read/create/update/delete
services.read/create/update/delete
appointments.read/create/update/cancel
reports.read
tenant.manage
```

Acessa **todas** as telas de tenant:

| Grupo | Telas |
|---|---|
| Principal | `/` (Visão Geral), `/agenda`, `/clientes`, `/profissionais`, `/servicos`, `/relatorios` |
| Gestão | `/equipe`, `/assinatura`, `/configuracoes` |
| Configurações (hub) | `/empresa`, `/perfis`, `/permissoes` |
| Conta | `/perfil` |
| Fluxo | `/onboarding`, `/configuracao-inicial` |

### 3.6 Tenant · Recepção (#5) — papel custom

Criar papel **"Recepção"** com:

```
customers.read, customers.create, customers.update,
appointments.read, appointments.create, appointments.update, appointments.cancel,
professionals.read, services.read
```

| Tela | Resultado |
|---|---|
| `/`, `/agenda`, `/clientes` | OK |
| `/profissionais`, `/servicos` | item **não aparece** na sidebar (falta `professionals.read`? — aparece; `services.read`? — aparece). Ver: sim. Criar/editar: **403** |
| `/relatorios` | item some da sidebar (sem `reports.read`); rota abre mas `GET /reports/overview` → **403** |
| `/equipe` | item aparece só com `tenant.members.read`; incluir usuário exige `tenant.members.create`, trocar papel `tenant.members.update`, cancelar convite / desativar membro `tenant.members.delete` — sem a permissão a ação some da UI e o backend responde **403** |
| `/empresa`, `/perfis`, `/permissoes` | cards do hub **escondidos**; rota abre mas `PUT/GET` → **403** |
| `/assinatura` | abre (hoje sem gate de permissão — limitação conhecida P2.1) |

### 3.7 Tenant · Operação/Profissional (#6) — papel custom

Criar papel **"Operação"** com:

```
appointments.read, appointments.update,
customers.read, services.read, professionals.read
```

Foco: agenda em modo consulta/atualização de status, sem criar agendamento
(`appointments.create` ausente → botão de novo agendamento chama endpoint que retorna **403**),
sem clientes/serviços além de leitura.

### 3.8 Tenant · Financeiro/Relatórios (#7) — papel custom

Criar papel **"Financeiro"** com:

```
reports.read, customers.read
```

| Tela | Resultado |
|---|---|
| `/`, `/relatorios` | OK |
| `/clientes` | ver, sem criar/editar/excluir |
| `/agenda`, `/servicos`, `/profissionais` | itens ausentes na sidebar; endpoints → 403 |

### 3.9 Tenant · Gerente (#8) — papel custom

Criar papel **"Gerente"** com **tudo menos `tenant.manage`**:

```
customers.read/create/update/delete
professionals.read/create/update/delete
services.read/create/update/delete
appointments.read/create/update/cancel
reports.read
```

Comporta‑se como ADMIN na operação, mas **sem** `/empresa`, `/perfis`, `/permissoes`
e sem administrar equipe.

---

## 4. Segmentos e planos (para o onboarding e o billing)

Só o segmento **`BARBERSHOP_SALON` (Barbearia / Salão)** é semeado (migration
`SeedBarbershopSalonVertical`). **Nenhum plano, preço de plano ou feature é semeado.**

Consequências para o teste:

- O passo **"Plano"** do `/onboarding` fica **vazio** até um Platform Admin criar,
  em `/admin`, ao menos um **plano** `IsActive` + `IsPublic` + `IsTrialEligible`
  com um **preço** (`/admin/billing` → preços).
- As telas `/assinatura` (tenant) e `/admin/assinaturas` só têm dados depois que uma
  empresa conclui o onboarding com um plano (gera `Subscription` em trial) **ou** que
  o Platform Admin cria uma assinatura manualmente em `/admin/assinaturas`.
- Você **pode** criar empresas sem onboarding, direto em `POST /api/v1/tenants`
  (é o que o script do Apêndice A faz) — só não terão assinatura.

Fluxo mínimo para exercitar billing:

1. `platform.admin` → `/admin` → cria **feature** (ex.: `REPORTS`), cria **plano**
   `basico` (marca público + elegível a trial), configura `plano×feature`.
2. `platform.admin` → `/admin/billing` → cria **preço** para `basico`
   (ex.: mensal, BRL, 49,90).
3. `novo.usuario` → `/onboarding` → escolhe segmento `Barbearia / Salão`, plano `basico`,
   conclui → vira ADMIN de uma empresa nova **com assinatura em trial**.
4. `/assinatura` (tenant) e `/admin/assinaturas` (plataforma) agora têm dados.

---

## 5. Matriz papel × tela (tenant)

| Tela | ADMIN | Gerente | Recepção | Operação | Financeiro |
|---|:--:|:--:|:--:|:--:|:--:|
| `/` Visão Geral | ✅ | ✅ | ✅ | ✅ | ✅ |
| `/agenda` | ✅ | ✅ | ✅ | 👁️ | — |
| `/clientes` | ✅ | ✅ | ✅ (sem excluir) | 👁️ | 👁️ |
| `/profissionais` | ✅ | ✅ | 👁️ | 👁️ | — |
| `/servicos` | ✅ | ✅ | 👁️ | 👁️ | — |
| `/relatorios` | ✅ | ✅ | — | — | ✅ |
| `/equipe` | ✅ | 🚪 | 🚪 | 🚪 | 🚪 |
| `/assinatura` | ✅ | ✅¹ | ✅¹ | ✅¹ | ✅¹ |
| `/configuracoes` · `/empresa` · `/perfis` · `/permissoes` | ✅ | — | — | — | — |
| `/perfil` | ✅ | ✅ | ✅ | ✅ | ✅ |

Legenda: ✅ acesso pleno · 👁️ somente leitura · 🚪 tela abre mas ações administrativas → 403 ·
— item some da sidebar e endpoints retornam 403 ·
¹ hoje qualquer membro abre `/assinatura` (limitação conhecida P2.1).

---

## Apêndice A — Script de provisionamento (PowerShell)

Cria as contas **#1, #2, #4–#8** e seus papéis via API. **Não** cria o Platform Admin
(ver Apêndice B) nem planos/preços (ver §4).

> O rate limit de `auth` é **10 req/min por IP**. O script espia 7s entre chamadas de
> identidade para não tomar 429. Rodar leva ~2 min.

```powershell
# scripts/seed-contas-teste.ps1
$ErrorActionPreference = 'Stop'
$api  = 'http://localhost:8080/api/v1'
$pass = 'Nexora-Teste-2026'

function Wait-Auth { Start-Sleep -Seconds 7 }

function New-Account([string]$email) {
  Wait-Auth
  $s = $null
  $r = Invoke-RestMethod "$api/identity/register" -Method Post -SessionVariable s `
       -ContentType 'application/json' -Body (@{ email = $email; password = $pass } | ConvertTo-Json)
  [pscustomobject]@{ Token = $r.accessToken; Session = $s }
}

function New-Tenant($acc, [string]$name, [string]$slug) {
  Invoke-RestMethod "$api/tenants" -Method Post -WebSession $acc.Session `
    -Headers @{ Authorization = "Bearer $($acc.Token)" } -ContentType 'application/json' `
    -Body (@{ name = $name; slug = $slug; timeZoneId = 'America/Sao_Paulo' } | ConvertTo-Json) | Out-Null
}

function Enter-Tenant($acc, [string]$slug) {
  Wait-Auth
  $r = Invoke-RestMethod "$api/t/$slug/session" -Method Post -WebSession $acc.Session `
       -Headers @{ Authorization = "Bearer $($acc.Token)" } -ContentType 'application/json' -Body '{}'
  $acc.Token = $r.accessToken   # agora tenant-scoped
}

function New-Role($acc, [string]$name, [string[]]$perms) {
  (Invoke-RestMethod "$api/tenant/roles" -Method Post -WebSession $acc.Session `
     -Headers @{ Authorization = "Bearer $($acc.Token)" } -ContentType 'application/json' `
     -Body (@{ name = $name; description = "Conta de teste"; permissions = $perms } | ConvertTo-Json)).id
}

function Set-OwnRole($acc, [string]$roleId) {
  $me = (Invoke-RestMethod "$api/tenant/members" -WebSession $acc.Session `
          -Headers @{ Authorization = "Bearer $($acc.Token)" })[0]
  Invoke-RestMethod "$api/tenant/members/$($me.id)/role" -Method Patch -WebSession $acc.Session `
    -Headers @{ Authorization = "Bearer $($acc.Token)" } -ContentType 'application/json' `
    -Body (@{ roleId = $roleId } | ConvertTo-Json) | Out-Null
}

# --- #1 usuário sem empresa
New-Account 'novo.usuario@nexora.test' | Out-Null

# --- #2 usuário com 2 empresas
$multi = New-Account 'multi.empresa@nexora.test'
New-Tenant $multi 'Alpha Co' 'alpha-co'
New-Tenant $multi 'Bravo Co' 'bravo-co'

# --- #4 tenant ADMIN
$admin = New-Account 'admin.barbearia@nexora.test'
New-Tenant $admin 'Barbearia Modelo' 'barbearia-modelo'

# --- #5..#8 papéis reduzidos (uma empresa/dono por papel)
$recepcao = @('customers.read','customers.create','customers.update',
              'appointments.read','appointments.create','appointments.update','appointments.cancel',
              'professionals.read','services.read')
$operacao = @('appointments.read','appointments.update','customers.read','services.read','professionals.read')
$financ   = @('reports.read','customers.read')
$gerente  = @('customers.read','customers.create','customers.update','customers.delete',
              'professionals.read','professionals.create','professionals.update','professionals.delete',
              'services.read','services.create','services.update','services.delete',
              'appointments.read','appointments.create','appointments.update','appointments.cancel',
              'reports.read')

$cases = @(
  @{ email='recepcao@nexora.test';   name='Salão Recepção';    slug='salao-recepcao';     role='Recepção';  perms=$recepcao },
  @{ email='operacao@nexora.test';   name='Estúdio Operação';  slug='estudio-operacao';   role='Operação';  perms=$operacao },
  @{ email='financeiro@nexora.test'; name='Clínica Financeiro'; slug='clinica-financeiro'; role='Financeiro'; perms=$financ  },
  @{ email='gerente@nexora.test';    name='Academia Gerência';  slug='academia-gerencia';  role='Gerente';   perms=$gerente  }
)

foreach ($c in $cases) {
  $acc = New-Account $c.email
  New-Tenant   $acc $c.name $c.slug
  Enter-Tenant $acc $c.slug
  $roleId = New-Role $acc $c.role $c.perms
  Set-OwnRole  $acc $roleId
  Write-Host "OK  $($c.email)  ->  $($c.slug)  ($($c.role))"
}

Write-Host "`nPronto. Faça login em http://localhost:4200 com a senha '$pass'."
Write-Host "Para os papéis reduzidos, o dono já foi rebaixado — basta logar."
```

---

## Apêndice B — Promover o Platform Admin

Não há endpoint de promoção. O `PlatformAdminBootstrapper` roda **no startup da API** e
promove o usuário cujo e‑mail está em `Administration:BootstrapAdminEmail`.

```bash
# 1. cadastre o usuário primeiro (pela UI /cadastro ou pela API)
curl -X POST http://localhost:8080/api/v1/identity/register `
  -H "Content-Type: application/json" `
  -d '{"email":"platform.admin@nexora.test","password":"Nexora-Teste-2026"}'

# 2. configure o e‑mail (user-secrets — não versiona)
cd backend/src/Nexora.Api
dotnet user-secrets set "Administration:BootstrapAdminEmail" "platform.admin@nexora.test"

# 3. reinicie a API  (o log "Platform Admin bootstrap applied to configured user" confirma)
dotnet run

# 4. o usuário precisa fazer login de novo para receber um token com platform.access
```

Alternativas ao user-secrets: variável de ambiente
`Administration__BootstrapAdminEmail=platform.admin@nexora.test`, ou a chave
`Administration:BootstrapAdminEmail` em `appsettings.Development.json` (este é versionado —
prefira user-secrets).

Para **remover** o acesso depois: limpe a configuração
(`dotnet user-secrets remove "Administration:BootstrapAdminEmail"`) — o bootstrapper só
adiciona o papel, não o retira; a remoção efetiva é manual no banco.
