#Requires -Version 5.1
<#
.SYNOPSIS
    Smoke mínimo de homologação: health, cadastro emitente, 1 NF-e Simples, teste 422.

.DESCRIPTION
    Lê scripts/config/homologacao.env (se existir) ou use parâmetros obrigatórios.
    Guia: docs/HOMOLOGACAO-RAPIDA.md

.EXAMPLE
    copy scripts\config\homologacao.env.example scripts\config\homologacao.env
    # edite homologacao.env
    .\scripts\smoke-minimo.ps1
#>
[CmdletBinding()]
param(
    [string] $Cnpj,
    [string] $Uf,
    [string] $CertificadoPath,
    [string] $CertificadoSenha,
    [string] $BaseUrl,
    [string] $ApiKey
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ConfigFile = Join-Path $PSScriptRoot "config\homologacao.env"

function Read-EnvFile {
    param([string] $Path)
    if (-not (Test-Path $Path)) { return }
    Get-Content $Path -Encoding UTF8 | ForEach-Object {
        $line = $_.Trim()
        if ($line -eq "" -or $line.StartsWith("#")) { return }
        $i = $line.IndexOf("=")
        if ($i -lt 1) { return }
        $k = $line.Substring(0, $i).Trim()
        $v = $line.Substring($i + 1).Trim().Trim('"').Trim("'")
        Set-Variable -Name $k -Value $v -Scope Script -Force
    }
}

Read-EnvFile (Join-Path $RepoRoot ".env")
Read-EnvFile $ConfigFile

if (-not $Cnpj) { $Cnpj = $script:SMOKE_CNPJ }
if (-not $Uf) { $Uf = if ($script:SMOKE_UF) { $script:SMOKE_UF } else { "RS" } }
if (-not $CertificadoPath) { $CertificadoPath = if ($script:SMOKE_CERTIFICADO_PATH) { $script:SMOKE_CERTIFICADO_PATH } else { "empresa-homologacao.pfx" } }
if (-not $CertificadoSenha) { $CertificadoSenha = $script:SMOKE_CERTIFICADO_SENHA }
if (-not $ApiKey) { $ApiKey = $env:API_KEY }
if (-not $BaseUrl) {
    if ($script:SMOKE_BASE_URL) { $BaseUrl = $script:SMOKE_BASE_URL }
    else {
        $port = if ($env:SERVICE_PORT) { $env:SERVICE_PORT } else { "5555" }
        $BaseUrl = "http://localhost:$port"
    }
}

if (-not $Cnpj -or $Cnpj -match "SUBSTITUA|00000000000000") {
    throw @"
Informe o CNPJ de homologação:
  1) copy scripts\config\homologacao.env.example scripts\config\homologacao.env
  2) Edite SMOKE_CNPJ e SMOKE_CERTIFICADO_SENHA
  ou: .\scripts\smoke-minimo.ps1 -Cnpj '12345678000199' -CertificadoSenha '...'
"@
}
if (-not $CertificadoSenha) {
    throw "Informe SMOKE_CERTIFICADO_SENHA em homologacao.env ou -CertificadoSenha"
}
if (-not $ApiKey) {
    throw "Defina API_KEY no .env da raiz do repositório"
}

Write-Host ""
Write-Host "Homologação rápida — CNPJ $Cnpj | UF $Uf" -ForegroundColor Cyan
Write-Host "Delegando para smoke-homologacao.ps1 ..." -ForegroundColor DarkGray
Write-Host ""

& (Join-Path $PSScriptRoot "smoke-homologacao.ps1") `
    -BaseUrl $BaseUrl `
    -ApiKey $ApiKey `
    -Cnpj $Cnpj `
    -Uf $Uf `
    -CertificadoPath $CertificadoPath `
    -CertificadoSenha $CertificadoSenha `
    -CadastrarEmitente `
    -EmitirNFe `
    -TestarTributacaoInvalida `
    -RepoRoot $RepoRoot

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Próximos passos:" -ForegroundColor Yellow
Write-Host "  1) Copie chave/protocolo do arquivo em scripts\smoke-output\" -ForegroundColor DarkGray
Write-Host "  2) Registre em PROGRESS.md (seção homologação)" -ForegroundColor DarkGray
Write-Host "  3) CRT 3: .\scripts\smoke-homologacao.ps1 -Cnpj $Cnpj -CertificadoSenha '***' -EmitirNFe -EmitirTodosRegimes" -ForegroundColor DarkGray
Write-Host ""
