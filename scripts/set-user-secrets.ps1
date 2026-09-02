<#
.SYNOPSIS
    Popula os user-secrets do projeto Nexora.Api (o "Manage User Secrets" do Visual Studio).

.DESCRIPTION
    Le os valores do arquivo .env na raiz do repo e grava os segredos que o backend
    precisa em Development via `dotnet user-secrets`. O store fica em
    %APPDATA%\Microsoft\UserSecrets\nexora-api-dev\secrets.json (UserSecretsId do .csproj),
    fora do controle de versao. Rode de novo sempre que o .env mudar.

.EXAMPLE
    pwsh ./scripts/set-user-secrets.ps1

.EXAMPLE
    # sobrescrevendo a connection string
    pwsh ./scripts/set-user-secrets.ps1 -ConnectionString 'Host=localhost;Port=5432;Database=saas_dev;Username=nexora;Password=xxx;Search Path=nexora'
#>
[CmdletBinding()]
param(
    [string]$EnvFile,
    [string]$ConnectionString,
    [string]$SigningKey,
    [string]$BootstrapAdminEmail,
    [string]$DbHost = 'localhost',
    [int]$DbPort = 5432
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $repoRoot 'backend/src/Nexora.Api'
if (-not $EnvFile) { $EnvFile = Join-Path $repoRoot '.env' }

# --- ler .env -------------------------------------------------------------
$env = @{}
if (Test-Path $EnvFile) {
    foreach ($line in Get-Content $EnvFile) {
        $t = $line.Trim()
        if (-not $t -or $t.StartsWith('#')) { continue }
        $i = $t.IndexOf('=')
        if ($i -lt 1) { continue }
        $env[$t.Substring(0, $i).Trim()] = $t.Substring($i + 1).Trim()
    }
    Write-Host "Lido $EnvFile" -ForegroundColor DarkGray
} else {
    Write-Warning "$EnvFile nao encontrado - usando apenas os parametros passados."
}

# --- montar valores finais ---------------------------------------------------
if (-not $SigningKey)          { $SigningKey          = $env['JWT_SIGNING_KEY'] }
if (-not $BootstrapAdminEmail) { $BootstrapAdminEmail = $env['PLATFORM_ADMIN_EMAIL'] }
if (-not $ConnectionString) {
    $db   = if ($env['POSTGRES_DB'])       { $env['POSTGRES_DB'] }       else { 'saas_dev' }
    $user = if ($env['POSTGRES_USER'])     { $env['POSTGRES_USER'] }     else { 'nexora' }
    $pass = $env['POSTGRES_PASSWORD']
    if (-not $pass) { throw 'POSTGRES_PASSWORD ausente no .env e -ConnectionString nao informado.' }
    $ConnectionString = "Host=$DbHost;Port=$DbPort;Database=$db;Username=$user;Password=$pass;Search Path=nexora"
}

if ($SigningKey -and $SigningKey.Length -lt 32) {
    throw "Identity:SigningKey precisa de >= 32 caracteres (tem $($SigningKey.Length))."
}

# --- gravar ----------------------------------------------------------------
$secrets = [ordered]@{
    'ConnectionStrings:NexoraDatabase'    = $ConnectionString
    'Identity:SigningKey'                 = $SigningKey
    'Administration:BootstrapAdminEmail'  = $BootstrapAdminEmail
}

Push-Location $project
try {
    dotnet user-secrets init | Out-Null
    foreach ($kv in $secrets.GetEnumerator()) {
        if ([string]::IsNullOrWhiteSpace($kv.Value)) {
            Write-Warning "Pulando $($kv.Key): sem valor."
            continue
        }
        dotnet user-secrets set $kv.Key $kv.Value | Out-Null
        $shown = if ($kv.Key -match 'Key|Password|Token') { '********' } else { $kv.Value }
        Write-Host ("  {0,-36} = {1}" -f $kv.Key, $shown) -ForegroundColor Green
    }
    Write-Host ''
    Write-Host 'Secrets atuais:' -ForegroundColor Cyan
    dotnet user-secrets list
}
finally {
    Pop-Location
}
