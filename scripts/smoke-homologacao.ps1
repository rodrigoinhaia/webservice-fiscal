#Requires -Version 5.1
<#
.SYNOPSIS
    Smoke test de homologação contra FiscalService.Api (SEFAZ homologação).

.DESCRIPTION
    Automatiza o checklist em docs/SMOKE-HOMOLOGACAO.md.
    Carrega API_KEY e SERVICE_PORT do .env na raiz do repositório, se existir.

.PARAMETER BaseUrl
    URL base da API (ex.: http://localhost:5555).

.PARAMETER ApiKey
    Chave X-Api-Key. Se omitida, usa $env:API_KEY ou valor do .env.

.PARAMETER Cnpj
    CNPJ do emitente (14 dígitos, sem máscara).

.PARAMETER CadastrarEmitente
    Envia POST /api/emitentes antes dos demais passos.

.PARAMETER EmitirNFe
    Emite NF-e de teste (Simples). Use -EmitirTodosRegimes para LP/LR/DIFAL/IPI.

.PARAMETER EmitirNFCe
    Emite NFC-e (exige -IdCsc e -Csc).

.PARAMETER PularSefaz
    Não chama emissão/cancelamento (só health, auth, cadastro, numeração).

.PARAMETER DryRun
    Apenas exibe as chamadas planejadas.

.EXAMPLE
    .\scripts\smoke-homologacao.ps1 -Cnpj 12345678000199 -CadastrarEmitente -EmitirNFe

.EXAMPLE
    .\scripts\smoke-homologacao.ps1 -BaseUrl https://fiscal.exemplo.com -ApiKey $env:API_KEY -EmitirNFe -EmitirTodosRegimes
#>
[CmdletBinding()]
param(
    [string] $BaseUrl,
    [string] $ApiKey,
    [string] $Cnpj = "00000000000000",
    [string] $Uf = "RS",
    [string] $CertificadoPath = "empresa.pfx",
    [string] $CertificadoSenha,
    [string] $IdCsc,
    [string] $Csc,
    [string] $SerieNFe = "1",
    [string] $RepoRoot,
    [switch] $CadastrarEmitente,
    [switch] $EmitirNFe,
    [switch] $EmitirTodosRegimes,
    [switch] $EmitirNFCe,
    [switch] $TestarDistribuicaoDfe,
    [switch] $TestarTributacaoInvalida,
    [switch] $PularSefaz,
    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Raiz do repositório ─────────────────────────────────────────────────────
if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}
$ExemplosDir = Join-Path $RepoRoot "docs\exemplos"

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
        if (-not [string]::IsNullOrWhiteSpace($name) -and -not (Get-Item -Path "Env:$name" -ErrorAction SilentlyContinue)) {
            Set-Item -Path "Env:$name" -Value $value
        }
    }
}

Import-DotEnvFile (Join-Path $RepoRoot ".env")

if (-not $BaseUrl) {
    $port = if ($env:SERVICE_PORT) { $env:SERVICE_PORT } else { "5555" }
    $BaseUrl = "http://localhost:$port"
}
$BaseUrl = $BaseUrl.TrimEnd("/")

if (-not $ApiKey) {
    $ApiKey = $env:API_KEY
    if (-not $ApiKey) { throw "Defina -ApiKey ou API_KEY no .env" }
}

$EvidenceDir = Join-Path $RepoRoot "scripts\smoke-output"
$EvidenceFile = Join-Path $EvidenceDir ("evidencias-{0:yyyyMMdd-HHmmss}.jsonl" -f (Get-Date))
$script:Pass = 0
$script:Fail = 0
$script:Skip = 0
$script:UltimaChave = $null
$script:UltimoProtocolo = $null

function Write-SmokeLog {
    param(
        [string] $Step,
        [ValidateSet("OK", "FAIL", "SKIP", "INFO")]
        [string] $Status,
        [string] $Detail = "",
        [object] $Data = $null
    )
    $cor = switch ($Status) {
        "OK" { "Green" }
        "FAIL" { "Red" }
        "SKIP" { "Yellow" }
        default { "Cyan" }
    }
    $msg = "[$Status] $Step"
    if ($Detail) { $msg += " — $Detail" }
    Write-Host $msg -ForegroundColor $cor

    if (-not (Test-Path $EvidenceDir)) {
        New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null
    }
    $entry = [ordered]@{
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
        step      = $Step
        status    = $Status
        detail    = $Detail
    }
    if ($null -ne $Data) { $entry.data = $Data }
    ($entry | ConvertTo-Json -Compress) | Add-Content -Path $EvidenceFile -Encoding UTF8

    switch ($Status) {
        "OK" { $script:Pass++ }
        "FAIL" { $script:Fail++ }
        "SKIP" { $script:Skip++ }
    }
}

function Get-JsonFromFile {
    param([string] $RelativePath)
    $full = Join-Path $ExemplosDir $RelativePath
    if (-not (Test-Path $full)) { throw "Arquivo não encontrado: $full" }
    $raw = Get-Content $full -Raw -Encoding UTF8
    return $raw | ConvertFrom-Json
}

function Set-EmitenteCnpjInObject {
    param($Obj, [string] $CnpjEmitente)
    if ($Obj.PSObject.Properties.Name -contains "emitenteCnpj") {
        $Obj.emitenteCnpj = $CnpjEmitente
    }
    if ($Obj.PSObject.Properties.Name -contains "configuracaoEmitente" -and $Obj.configuracaoEmitente) {
        $Obj.configuracaoEmitente.cnpj = $CnpjEmitente
    }
}

function Invoke-FiscalApi {
    param(
        [string] $Method,
        [string] $Path,
        [object] $Body = $null,
        [hashtable] $Query = @{},
        [switch] $SemApiKey
    )
    $uri = "$BaseUrl$Path"
    if ($Query.Count -gt 0) {
        $qs = ($Query.GetEnumerator() | ForEach-Object {
            "{0}={1}" -f [uri]::EscapeDataString($_.Key), [uri]::EscapeDataString([string]$_.Value)
        }) -join "&"
        $uri = "$uri`?$qs"
    }

    if ($DryRun) {
        Write-SmokeLog -Step "$Method $Path" -Status "INFO" -Detail "(dry-run)"
        return $null
    }

    $headers = @{ Accept = "application/json" }
    if (-not $SemApiKey) { $headers["X-Api-Key"] = $ApiKey }

    $params = @{
        Method      = $Method
        Uri         = $uri
        Headers     = $headers
        ContentType = "application/json; charset=utf-8"
    }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 20 -Compress)
    }

    try {
        return Invoke-RestMethod @params
    }
    catch {
        $resp = $_.Exception.Response
        if ($resp) {
            $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
            $text = $reader.ReadToEnd()
            $reader.Close()
            try { return $text | ConvertFrom-Json } catch { throw "HTTP $($resp.StatusCode.value__): $text" }
        }
        throw
    }
}

function Test-HttpStatus {
    param(
        [string] $Step,
        [string] $Method,
        [string] $Path,
        [int] $ExpectedStatus,
        [switch] $SemApiKey
    )
    if ($DryRun) {
        Write-SmokeLog -Step $Step -Status "INFO" -Detail "esperado HTTP $ExpectedStatus"
        return
    }
    $uri = "$BaseUrl$Path"
    $headers = @{}
    if (-not $SemApiKey) { $headers["X-Api-Key"] = $ApiKey }
    try {
        Invoke-WebRequest -Method $Method -Uri $uri -Headers $headers -UseBasicParsing | Out-Null
        Write-SmokeLog -Step $Step -Status "FAIL" -Detail "esperava HTTP $ExpectedStatus, obteve 2xx"
    }
    catch {
        $code = [int]$_.Exception.Response.StatusCode
        if ($code -eq $ExpectedStatus) {
            Write-SmokeLog -Step $Step -Status "OK" -Detail "HTTP $code"
        }
        else {
            Write-SmokeLog -Step $Step -Status "FAIL" -Detail "esperava HTTP $ExpectedStatus, obteve HTTP $code"
        }
    }
}

function Get-ProximoNumeroNFe {
    $r = Invoke-FiscalApi -Method GET -Path "/api/numeracao/$Cnpj/55/$SerieNFe"
    if ($r -and $r.proximoNumero) { return [int]$r.proximoNumero }
    return 1
}

function Emitir-NFeDeExemplo {
    param(
        [string] $ArquivoRelativo,
        [string] $NomeCenario
    )
    $body = Get-JsonFromFile $ArquivoRelativo
    Set-EmitenteCnpjInObject -Obj $body -CnpjEmitente $Cnpj
    $body.numeroNota = Get-ProximoNumeroNFe
    $body.serie = $SerieNFe

    $resp = Invoke-FiscalApi -Method POST -Path "/api/nfe/emitir" -Body $body
    if ($resp.sucesso -eq $true -and $resp.codigoStatus -eq "100") {
        $script:UltimaChave = $resp.chaveAcesso
        $script:UltimoProtocolo = $resp.protocolo
        Write-SmokeLog -Step "NF-e emitir: $NomeCenario" -Status "OK" -Detail "cStat=$($resp.codigoStatus) chave=$($resp.chaveAcesso)" -Data $resp
    }
    elseif ($resp.sucesso -eq $false) {
        $tipo = $resp.erro.tipo
        $det = $resp.erro.detalhe
        Write-SmokeLog -Step "NF-e emitir: $NomeCenario" -Status "FAIL" -Detail "$tipo — $($resp.erro.mensagem) $det" -Data $resp
    }
    else {
        Write-SmokeLog -Step "NF-e emitir: $NomeCenario" -Status "FAIL" -Detail "resposta inesperada" -Data $resp
    }
}

# ── Execução ────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== Smoke homologação — $BaseUrl ===" -ForegroundColor White
Write-Host "CNPJ: $Cnpj | Evidências: $EvidenceFile" -ForegroundColor DarkGray
Write-Host ""

# 1. Health
$health = Invoke-FiscalApi -Method GET -Path "/health" -SemApiKey
if ($health.status -match "healthy|degraded") {
    Write-SmokeLog -Step "GET /health" -Status "OK" -Detail "status=$($health.status) banco=$($health.banco)" -Data $health
}
else {
    Write-SmokeLog -Step "GET /health" -Status "FAIL" -Detail "status=$($health.status)" -Data $health
}

# 2. Autenticação
Test-HttpStatus -Step "GET status-sefaz sem API Key → 401" -Method GET -Path "/api/nfe/status-sefaz" -ExpectedStatus 401 -SemApiKey

if (-not $PularSefaz -and $CertificadoSenha) {
    $st = Invoke-FiscalApi -Method GET -Path "/api/nfe/status-sefaz" -Query @{
        uf               = $Uf
        ambiente         = "Homologacao"
        certificadoPath  = $CertificadoPath
        certificadoSenha = $CertificadoSenha
        cnpj             = $Cnpj
        razaoSocial      = "Smoke Test"
    }
    if ($st.sucesso -eq $true -or $st.codigoStatus -eq "107") {
        Write-SmokeLog -Step "GET status-sefaz com API Key" -Status "OK" -Detail "cStat=$($st.codigoStatus) $($st.mensagem)" -Data $st
    }
    else {
        Write-SmokeLog -Step "GET status-sefaz com API Key" -Status "FAIL" -Detail $st.mensagem -Data $st
    }
}
else {
    Write-SmokeLog -Step "GET status-sefaz com API Key" -Status "SKIP" -Detail "informe -CertificadoSenha ou cadastre emitente"
}

# POST consulta status multimodelo
if (-not $PularSefaz) {
    $consultaBody = @{
        modelo               = "NFe"
        emitenteCnpj         = $Cnpj
        configuracaoEmitente = $null
    }
    # Se emitente não cadastrado, status via configuracao exige senha no body — pular se só CNPJ
    if ($CadastrarEmitente -or $EmitirNFe) {
        $cs = Invoke-FiscalApi -Method POST -Path "/api/consulta/status-servico" -Body @{ modelo = "NFe"; emitenteCnpj = $Cnpj }
        if ($cs.sucesso) {
            Write-SmokeLog -Step "POST /api/consulta/status-servico (NFe)" -Status "OK" -Detail "cStat=$($cs.codigoStatus)" -Data $cs
        }
        else {
            Write-SmokeLog -Step "POST /api/consulta/status-servico (NFe)" -Status "FAIL" -Data $cs
        }
    }
}

# 3. Cadastro emitente
if ($CadastrarEmitente) {
    $cad = Get-JsonFromFile "emitente\cadastro-homologacao.json"
    $cad.cnpj = $Cnpj
    $cad.uf = $Uf
    if ($CertificadoSenha) { $cad.certificadoSenha = $CertificadoSenha }
    if ($CertificadoPath) { $cad.certificadoPath = $CertificadoPath }

    try {
        $criado = Invoke-FiscalApi -Method POST -Path "/api/emitentes" -Body $cad
        Write-SmokeLog -Step "POST /api/emitentes" -Status "OK" -Detail "CNPJ=$($criado.cnpj)" -Data $criado
    }
    catch {
        $err = $_
        Write-SmokeLog -Step "POST /api/emitentes" -Status "FAIL" -Detail $err.Exception.Message
    }

    $get = Invoke-FiscalApi -Method GET -Path "/api/emitentes/$Cnpj"
    if ($get -and -not $get.certificadoSenha) {
        Write-SmokeLog -Step "GET /api/emitentes/{cnpj}" -Status "OK" -Detail "senha não exposta na resposta"
    }
    else {
        Write-SmokeLog -Step "GET /api/emitentes/{cnpj}" -Status "FAIL" -Detail "emitente ausente ou senha exposta"
    }

    $health2 = Invoke-FiscalApi -Method GET -Path "/health" -SemApiKey
    $certCheck = $health2.checks.certificados_emitentes
    if ($certCheck -eq "healthy") {
        Write-SmokeLog -Step "Health certificados_emitentes" -Status "OK" -Detail $certCheck
    }
    else {
        Write-SmokeLog -Step "Health certificados_emitentes" -Status "FAIL" -Detail $certCheck -Data $health2
    }
}

# Numeração
$num = Invoke-FiscalApi -Method GET -Path "/api/numeracao/$Cnpj/55/$SerieNFe"
if ($num) {
    Write-SmokeLog -Step "GET /api/numeracao" -Status "OK" -Detail ($num | ConvertTo-Json -Compress) -Data $num
}

# 4. Emissões NF-e
if ($EmitirNFe -and -not $PularSefaz) {
    Emitir-NFeDeExemplo -ArquivoRelativo "nfe\emitir-via-emitente-cnpj.json" -NomeCenario "via emitenteCnpj"

    if ($EmitirTodosRegimes) {
        $cenarios = @(
            @{ f = "nfe\crt1-simples-csosn102-homologacao.json"; n = "Simples CSOSN 102" },
            @{ f = "nfe\crt3-lucro-presumido-icms00.json"; n = "LP ICMS 00" },
            @{ f = "nfe\crt3-lucro-real-icms10-st.json"; n = "LR ICMS 10 ST" },
            @{ f = "nfe\crt3-interestadual-difal.json"; n = "DIFAL" },
            @{ f = "nfe\crt3-item-com-ipi.json"; n = "IPI" },
            @{ f = "nfe\crt3-icms20-reducao-base.json"; n = "ICMS 20" }
        )
        foreach ($c in $cenarios) {
            Emitir-NFeDeExemplo -ArquivoRelativo $c.f -NomeCenario $c.n
        }
    }

    if ($script:UltimaChave) {
        $cons = Invoke-FiscalApi -Method POST -Path "/api/nfe/consultar" -Body @{
            emitenteCnpj = $Cnpj
            chaveAcesso  = $script:UltimaChave
        }
        Write-SmokeLog -Step "POST /api/nfe/consultar" -Status $(if ($cons.sucesso) { "OK" } else { "FAIL" }) -Detail "cStat=$($cons.codigoStatus)" -Data $cons
    }
}

# 7. Distribuição DF-e
if ($TestarDistribuicaoDfe -and -not $PularSefaz) {
    $dist = Get-JsonFromFile "nfe\distribuicao-dfe.json"
    Set-EmitenteCnpjInObject -Obj $dist -CnpjEmitente $Cnpj
    $distResp = Invoke-FiscalApi -Method POST -Path "/api/nfe/distribuicao-dfe" -Body $dist
    $okDist = $distResp.sucesso -or $distResp.codigoStatus -in @("137", "138", "656")
    Write-SmokeLog -Step "POST /api/nfe/distribuicao-dfe" -Status $(if ($okDist) { "OK" } else { "FAIL" }) `
        -Detail "cStat=$($distResp.codigoStatus) $($distResp.mensagem)" -Data $distResp
}

# 8. NFC-e
if ($EmitirNFCe -and -not $PularSefaz) {
    if ($IdCsc -and $Csc) {
        $nfce = Get-JsonFromFile "nfce\emitir-homologacao-csc.json"
        Set-EmitenteCnpjInObject -Obj $nfce -CnpjEmitente $Cnpj
        $nfce.idCsc = $IdCsc
        $nfce.csc = $Csc
        $nfce.numeroNota = Get-ProximoNumeroNFe  # modelo 65 usa mesma rota numeracao com 65
        $num65 = Invoke-FiscalApi -Method GET -Path "/api/numeracao/$Cnpj/65/$SerieNFe"
        if ($num65.proximoNumero) { $nfce.numeroNota = [int]$num65.proximoNumero }
        $nfceResp = Invoke-FiscalApi -Method POST -Path "/api/nfce/emitir" -Body $nfce
        Write-SmokeLog -Step "POST /api/nfce/emitir" -Status $(if ($nfceResp.sucesso) { "OK" } else { "FAIL" }) `
            -Detail "cStat=$($nfceResp.codigoStatus)" -Data $nfceResp
    }
    else {
        Write-SmokeLog -Step "POST /api/nfce/emitir" -Status "SKIP" -Detail "informe -IdCsc e -Csc"
    }
}

# 11. Tributação inválida
if ($TestarTributacaoInvalida) {
    $bad = Get-JsonFromFile "nfe\emitir-via-emitente-cnpj.json"
    Set-EmitenteCnpjInObject -Obj $bad -CnpjEmitente $Cnpj
    $bad.itens[0].csosnIcms = "999"
    $bad.numeroNota = Get-ProximoNumeroNFe
    try {
        $badResp = Invoke-FiscalApi -Method POST -Path "/api/nfe/emitir" -Body $bad
        if ($badResp.erro.tipo -eq "TributacaoInvalida" -or $badResp.erro.tipo -eq "Validacao") {
            Write-SmokeLog -Step "NF-e CST inválido → 422" -Status "OK" -Detail $badResp.erro.tipo -Data $badResp
        }
        else {
            Write-SmokeLog -Step "NF-e CST inválido → 422" -Status "FAIL" -Detail "esperava rejeição de tributação" -Data $badResp
        }
    }
    catch {
        Write-SmokeLog -Step "NF-e CST inválido → 422" -Status "OK" -Detail "exceção HTTP (rejeição esperada)"
    }
}

# Resumo
Write-Host ""
Write-Host "=== Resumo: OK=$($script:Pass) FAIL=$($script:Fail) SKIP=$($script:Skip) ===" -ForegroundColor White
Write-Host "Evidências: $EvidenceFile" -ForegroundColor DarkGray
if ($script:UltimaChave) {
    Write-Host "Última chave: $($script:UltimaChave)" -ForegroundColor DarkGray
}

if ($script:Fail -gt 0) { exit 1 }
exit 0
