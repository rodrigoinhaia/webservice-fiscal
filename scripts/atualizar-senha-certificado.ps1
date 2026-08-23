#Requires -Version 5.1
<#
.SYNOPSIS
    Re-salva a senha do certificado A1 no emitente (re-criptografa com /app/keys atual).

.DESCRIPTION
    Use após redeploy quando health mostra certificados_emitentes=degraded
    e a emissão falha com "key was not found in the key ring".

.EXAMPLE
    .\scripts\atualizar-senha-certificado.ps1 -Senha "sua-senha-pfx"

.EXAMPLE
    # Lê SMOKE_BASE_URL, SMOKE_API_KEY, SMOKE_CNPJ de scripts/config/producao.env
    $env:CERTIFICADO_SENHA = "sua-senha"
    .\scripts\atualizar-senha-certificado.ps1
#>
[CmdletBinding()]
param(
    [string] $BaseUrl,
    [string] $ApiKey,
    [string] $Cnpj,
    [string] $Senha
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ConfigFile = Join-Path $PSScriptRoot "config\producao.env"

function Import-DotEnvFile {
    param([string] $Path)
    if (-not (Test-Path $Path)) { return }
    Get-Content $Path -Encoding UTF8 | ForEach-Object {
        $line = $_.Trim()
        if ($line -eq "" -or $line.StartsWith("#")) { return }
        $eq = $line.IndexOf("=")
        if ($eq -lt 1) { return }
        $name = $line.Substring(0, $eq).Trim()
        $value = $line.Substring($eq + 1).Trim().Trim('"').Trim("'")
        Set-Variable -Name $name -Value $value -Scope Script -Force
    }
}

Import-DotEnvFile $ConfigFile

if (-not $BaseUrl) {
    if (Test-Path variable:script:SMOKE_BASE_URL) { $BaseUrl = $script:SMOKE_BASE_URL }
    else { throw "Defina SMOKE_BASE_URL em producao.env ou -BaseUrl" }
}
if (-not $ApiKey) {
    if (Test-Path variable:script:SMOKE_API_KEY) { $ApiKey = $script:SMOKE_API_KEY }
    else { throw "Defina SMOKE_API_KEY em producao.env ou -ApiKey" }
}
if (-not $Cnpj) {
    if (Test-Path variable:script:SMOKE_CNPJ) { $Cnpj = $script:SMOKE_CNPJ }
    else { throw "Defina SMOKE_CNPJ em producao.env ou -Cnpj" }
}
if (-not $Senha) {
    $Senha = $env:CERTIFICADO_SENHA
}
if (-not $Senha) {
    $secure = Read-Host "Senha do certificado .pfx" -AsSecureString
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { $Senha = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}
if ([string]::IsNullOrWhiteSpace($Senha)) { throw "Senha nao informada." }

$BaseUrl = $BaseUrl.TrimEnd("/")
$headers = @{
    "X-Api-Key"      = $ApiKey
    "Content-Type"   = "application/json"
    "Accept"         = "application/json"
}
$body = @{
    certificadoSenha         = $Senha
    validarCnpjCertificado = $true
} | ConvertTo-Json

Write-Host "Atualizando senha do certificado para CNPJ $Cnpj em $BaseUrl ..." -ForegroundColor Cyan

try {
    $resp = Invoke-RestMethod -Method PUT -Uri "$BaseUrl/api/emitentes/$Cnpj" -Headers $headers -Body $body
    Write-Host "OK - atualizadoEm: $($resp.atualizadoEm)" -ForegroundColor Green
}
catch {
    $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
    $text = $reader.ReadToEnd()
    $reader.Close()
    Write-Host "FALHA: $text" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Verificando health..." -ForegroundColor DarkGray
Start-Sleep -Seconds 1
$health = Invoke-RestMethod -Uri "$BaseUrl/health"
Write-Host "status=$($health.status) certificados=$($health.checks.certificados_emitentes)" -ForegroundColor $(if ($health.checks.certificados_emitentes -eq "healthy") { "Green" } else { "Yellow" })

if ($health.checks.certificados_emitentes -ne "healthy") {
    Write-Host "Ainda degraded — confira senha do PFX e arquivo em /app/certificados." -ForegroundColor Yellow
    exit 1
}

exit 0
