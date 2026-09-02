<#
.SYNOPSIS
  Provisiona as contas de teste do Nexora via API (ambiente local).
.DESCRIPTION
  Cria as contas #1, #2, #4-#8 descritas em docs/CONTAS-DE-TESTE.md e seus papeis.
  NAO cria o Platform Admin (ver Apendice B do doc) nem planos/precos (ver secao 4).
  Requer a API rodando em http://localhost:8080 (Development).

  O rate limit de "auth" e 10 req/min por IP; o script espera 7s entre chamadas de
  identidade para nao tomar 429. Execucao leva ~2 min.
.EXAMPLE
  pwsh ./scripts/seed-contas-teste.ps1
#>
[CmdletBinding()]
param(
  [string] $Api = 'http://localhost:8080/api/v1',
  [string] $Password = 'Nexora-Teste-2026'
)

$ErrorActionPreference = 'Stop'

function Wait-Auth { Start-Sleep -Seconds 7 }

function New-Account([string]$email) {
  Wait-Auth
  $session = $null
  $response = Invoke-RestMethod "$Api/identity/register" -Method Post -SessionVariable session `
    -ContentType 'application/json' -Body (@{ email = $email; password = $Password } | ConvertTo-Json)
  [pscustomobject]@{ Token = $response.accessToken; Session = $session }
}

function New-Tenant($account, [string]$name, [string]$slug) {
  Invoke-RestMethod "$Api/tenants" -Method Post -WebSession $account.Session `
    -Headers @{ Authorization = "Bearer $($account.Token)" } -ContentType 'application/json' `
    -Body (@{ name = $name; slug = $slug; timeZoneId = 'America/Sao_Paulo' } | ConvertTo-Json) | Out-Null
}

function Enter-Tenant($account, [string]$slug) {
  Wait-Auth
  $response = Invoke-RestMethod "$Api/t/$slug/session" -Method Post -WebSession $account.Session `
    -Headers @{ Authorization = "Bearer $($account.Token)" } -ContentType 'application/json' -Body '{}'
  $account.Token = $response.accessToken   # agora tenant-scoped
}

function New-Role($account, [string]$name, [string[]]$permissions) {
  (Invoke-RestMethod "$Api/tenant/roles" -Method Post -WebSession $account.Session `
    -Headers @{ Authorization = "Bearer $($account.Token)" } -ContentType 'application/json' `
    -Body (@{ name = $name; description = 'Conta de teste'; permissions = $permissions } | ConvertTo-Json)).id
}

function Set-OwnRole($account, [string]$roleId) {
  $me = (Invoke-RestMethod "$Api/tenant/members" -WebSession $account.Session `
    -Headers @{ Authorization = "Bearer $($account.Token)" })[0]
  Invoke-RestMethod "$Api/tenant/members/$($me.id)/role" -Method Patch -WebSession $account.Session `
    -Headers @{ Authorization = "Bearer $($account.Token)" } -ContentType 'application/json' `
    -Body (@{ roleId = $roleId } | ConvertTo-Json) | Out-Null
}

Write-Host "Provisionando contas de teste em $Api ..." -ForegroundColor Cyan

# #1 usuario sem empresa
New-Account 'novo.usuario@nexora.test' | Out-Null
Write-Host 'OK  novo.usuario@nexora.test  (sem empresa)'

# #2 usuario com 2 empresas
$multi = New-Account 'multi.empresa@nexora.test'
New-Tenant $multi 'Alpha Co' 'alpha-co'
New-Tenant $multi 'Bravo Co' 'bravo-co'
Write-Host 'OK  multi.empresa@nexora.test  ->  alpha-co, bravo-co'

# #4 tenant ADMIN
$admin = New-Account 'admin.barbearia@nexora.test'
New-Tenant $admin 'Barbearia Modelo' 'barbearia-modelo'
Write-Host 'OK  admin.barbearia@nexora.test  ->  barbearia-modelo  (ADMIN)'

# #5..#8 papeis reduzidos (uma empresa/dono por papel)
$recepcao = @('customers.read', 'customers.create', 'customers.update',
  'appointments.read', 'appointments.create', 'appointments.update', 'appointments.cancel',
  'professionals.read', 'services.read')
$operacao = @('appointments.read', 'appointments.update', 'customers.read', 'services.read', 'professionals.read')
$financeiro = @('reports.read', 'customers.read')
$gerente = @('customers.read', 'customers.create', 'customers.update', 'customers.delete',
  'professionals.read', 'professionals.create', 'professionals.update', 'professionals.delete',
  'services.read', 'services.create', 'services.update', 'services.delete',
  'appointments.read', 'appointments.create', 'appointments.update', 'appointments.cancel',
  'reports.read')

$cases = @(
  @{ email = 'recepcao@nexora.test';   name = 'Salão Recepção';     slug = 'salao-recepcao';     role = 'Recepção';   perms = $recepcao },
  @{ email = 'operacao@nexora.test';   name = 'Estúdio Operação';   slug = 'estudio-operacao';   role = 'Operação';   perms = $operacao },
  @{ email = 'financeiro@nexora.test'; name = 'Clínica Financeiro'; slug = 'clinica-financeiro'; role = 'Financeiro'; perms = $financeiro },
  @{ email = 'gerente@nexora.test';    name = 'Academia Gerência';  slug = 'academia-gerencia';  role = 'Gerente';    perms = $gerente }
)

foreach ($case in $cases) {
  $account = New-Account $case.email
  New-Tenant   $account $case.name $case.slug
  Enter-Tenant $account $case.slug
  $roleId = New-Role $account $case.role $case.perms
  Set-OwnRole  $account $roleId
  Write-Host "OK  $($case.email)  ->  $($case.slug)  ($($case.role))"
}

Write-Host ''
Write-Host "Pronto. Login em http://localhost:4200 com a senha '$Password'." -ForegroundColor Green
Write-Host 'Platform Admin: ver docs/CONTAS-DE-TESTE.md, Apendice B.'
