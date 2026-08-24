#Requires -Version 5.1
<#
.SYNOPSIS
    Smoke test de PRODUÇÃO — emite e cancela documento fiscal real.

.DESCRIPTION
    Valida o FiscalService em ambiente de produção (SEFAZ / NFS-e Nacional).
    Fluxo por modelo: health → emitente → numeração → emitir → consultar → cancelar.

    Cliente de teste (tomador/destinatário): SDBR SOLUCOES DIGITAIS LTDA
    CNPJ 53.658.565/0001-27 — dados do comprovante CNPJ.

    ATENÇÃO: gera documentos fiscais REAIS. Use -ConfirmarProducao para executar
    emissão/cancelamento. Sem essa flag, apenas pré-checks (health, emitente, numeração).

.PARAMETER BaseUrl
    URL base da API (ex.: https://fiscal.exemplo.com).

.PARAMETER ApiKey
    Chave X-Api-Key.

.PARAMETER Cnpj
    CNPJ do emitente (14 dígitos).

.PARAMETER Modelo
    NFe | NFCe | NFSe | Todos

.PARAMETER ConfirmarProducao
    Obrigatório para emitir/cancelar em produção.

.PARAMETER IdCsc
    Id CSC produção (NFC-e). Opcional se cadastrado no emitente.

.PARAMETER Csc
    CSC produção (NFC-e). Opcional se cadastrado no emitente.

.PARAMETER DryRun
    Apenas exibe as chamadas planejadas.

.EXAMPLE
    # Pré-checks (sem emissão)
    .\scripts\smoke-producao.ps1

.EXAMPLE
    # NFS-e: emitir e cancelar (após configurar producao.env)
    .\scripts\smoke-producao.ps1 -ConfirmarProducao

.EXAMPLE
    # NF-e + NFC-e + NFS-e
    .\scripts\smoke-producao.ps1 -Modelo Todos -ConfirmarProducao
#>
[CmdletBinding()]
param(
    [string] $BaseUrl,
    [string] $ApiKey,
    [string] $Cnpj,
    [ValidateSet("NFe", "NFCe", "NFSe", "Todos")]
    [string] $Modelo = "NFSe",
    [string] $SerieNFe = "3",
    [string] $SerieNFCe = "2",
    [string] $SerieNFSe = "1",
    [string] $IdCsc,
    [string] $Csc,
    [string] $RepoRoot,
    [switch] $ConfirmarProducao,
    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}
$ExemplosDir = Join-Path $RepoRoot "docs\exemplos\producao"
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

Import-DotEnvFile (Join-Path $RepoRoot ".env")
Import-DotEnvFile $ConfigFile

if (-not $BaseUrl) {
    if (Test-Path variable:script:SMOKE_BASE_URL) { $BaseUrl = $script:SMOKE_BASE_URL }
    else {
        $port = if ($env:SERVICE_PORT) { $env:SERVICE_PORT } else { "5555" }
        $BaseUrl = "http://localhost:$port"
    }
}
$BaseUrl = $BaseUrl.TrimEnd("/")

if (-not $ApiKey) {
    $ApiKey = if (Test-Path variable:script:SMOKE_API_KEY) { $script:SMOKE_API_KEY } else { $env:API_KEY }
}
if (-not $ApiKey) { throw "Defina SMOKE_API_KEY em scripts/config/producao.env ou -ApiKey" }

if (-not $Cnpj) {
    if (Test-Path variable:script:SMOKE_CNPJ) { $Cnpj = $script:SMOKE_CNPJ }
}
if (-not $Cnpj) { throw "Defina SMOKE_CNPJ em producao.env ou -Cnpj" }

if (Test-Path variable:script:SMOKE_SERIE_NFE) { $SerieNFe = $script:SMOKE_SERIE_NFE }
if (Test-Path variable:script:SMOKE_SERIE_NFCE) { $SerieNFCe = $script:SMOKE_SERIE_NFCE }
if (Test-Path variable:script:SMOKE_SERIE_NFSE) { $SerieNFSe = $script:SMOKE_SERIE_NFSE }
if (Test-Path variable:script:SMOKE_MODELO) { $Modelo = $script:SMOKE_MODELO }
if (-not $IdCsc -and (Test-Path variable:script:SMOKE_ID_CSC)) { $IdCsc = $script:SMOKE_ID_CSC }
if (-not $Csc -and (Test-Path variable:script:SMOKE_CSC)) { $Csc = $script:SMOKE_CSC }

$EvidenceDir = Join-Path $RepoRoot "scripts\smoke-output"
$EvidenceFile = Join-Path $EvidenceDir ("producao-{0:yyyyMMdd-HHmmss}.jsonl" -f (Get-Date))
$script:Pass = 0
$script:Fail = 0
$script:Skip = 0
$JustificativaCancelamento = "Teste de validacao do webservice fiscal em producao - cancelamento imediato."
$NfseMotivoCancelamento = "Teste de validacao do webservice fiscal em producao - cancelamento imediato."

function Write-SmokeLog {
    param(
        [string] $Step,
        [ValidateSet("OK", "FAIL", "SKIP", "INFO", "WARN")]
        [string] $Status,
        [string] $Detail = "",
        [object] $Data = $null
    )
    $cor = switch ($Status) {
        "OK" { "Green" }
        "FAIL" { "Red" }
        "SKIP" { "Yellow" }
        "WARN" { "DarkYellow" }
        default { "Cyan" }
    }
    $msg = "[$Status] $Step"
    if ($Detail) { $msg += " - $Detail" }
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
    ($entry | ConvertTo-Json -Compress -Depth 12) | Add-Content -Path $EvidenceFile -Encoding UTF8

    switch ($Status) {
        "OK" { $script:Pass++ }
        "FAIL" { $script:Fail++ }
        "SKIP" { $script:Skip++ }
    }
}

function Get-JsonFromFile {
    param([string] $FileName)
    $full = Join-Path $ExemplosDir $FileName
    if (-not (Test-Path $full)) { throw "Arquivo nao encontrado: $full" }
    return (Get-Content $full -Raw -Encoding UTF8) | ConvertFrom-Json
}

function Set-EmitenteCnpjInObject {
    param($Obj, [string] $CnpjEmitente)
    if ($Obj.PSObject.Properties.Name -contains "emitenteCnpj") {
        $Obj.emitenteCnpj = $CnpjEmitente
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

function Get-ProximoNumero {
    param([string] $ModeloCodigo, [string] $Serie)
    $r = Invoke-FiscalApi -Method GET -Path "/api/numeracao/$Cnpj/$ModeloCodigo/$Serie" -Query @{
        ambiente = "Producao"
        reservar = "true"
    }
    if ($r -and $r.proximoNumero) { return [int]$r.proximoNumero }
    throw "Nao foi possivel obter proximo numero para modelo $ModeloCodigo serie $Serie"
}

function Get-Prop {
    param($Obj, [string] $Name, $Default = $null)
    if ($null -eq $Obj) { return $Default }
    $p = $Obj.PSObject.Properties[$Name]
    if ($null -eq $p) { return $Default }
    return $p.Value
}

function Test-RespostaAutorizada {
    param($Resp, [string[]] $CodigosOk = @("100"))
    $cStat = [string](Get-Prop $Resp "codigoStatus")
    return ($Resp -and (Get-Prop $Resp "sucesso") -eq $true -and ($CodigosOk -contains $cStat))
}

function Test-CicloEmitirCancelar {
    param(
        [string] $NomeModelo,
        [string] $ArquivoEmitir,
        [scriptblock] $MontarCancelamento,
        [string] $PathConsultar = $null,
        [scriptblock] $MontarConsulta = $null,
        [scriptblock] $AjustarEmitir = $null
    )

    Write-Host ""
    Write-Host "--- $NomeModelo ---" -ForegroundColor White

    $body = Get-JsonFromFile $ArquivoEmitir
    Set-EmitenteCnpjInObject -Obj $body -CnpjEmitente $Cnpj
    if ($AjustarEmitir) { & $AjustarEmitir $body }

    $emitResp = Invoke-FiscalApi -Method POST -Path "/api/$($NomeModelo.ToLower())/emitir" -Body $body
    if (-not (Test-RespostaAutorizada $emitResp)) {
        $erro = Get-Prop $emitResp "erro"
        $det = if ($erro) { "$(Get-Prop $erro 'tipo'): $(Get-Prop $erro 'mensagem')" } else { "resposta inesperada" }
        Write-SmokeLog -Step "$NomeModelo emitir" -Status "FAIL" -Detail $det -Data $emitResp
        return
    }

    $chave = Get-Prop $emitResp "chaveAcesso"
    $protocolo = Get-Prop $emitResp "protocolo"
    Write-SmokeLog -Step "$NomeModelo emitir" -Status "OK" -Detail "chave=$chave protocolo=$protocolo" -Data $emitResp

    if ($PathConsultar -and $MontarConsulta) {
        $consBody = & $MontarConsulta $chave
        $consResp = $null
        $okCons = $false
        # ADN/distribuição DF-e costuma atrasar; cancelar cedo demais pode impedir a indexação
        Start-Sleep -Seconds 5
        for ($tentativa = 1; $tentativa -le 8; $tentativa++) {
            if ($tentativa -gt 1) { Start-Sleep -Seconds 5 }
            $consResp = Invoke-FiscalApi -Method POST -Path $PathConsultar -Body $consBody
            $cStatCons = [string](Get-Prop $consResp "codigoStatus")
            $okCons = ((Get-Prop $consResp "sucesso") -eq $true) -or ($cStatCons -eq "DOCUMENTOS_LOCALIZADOS")
            if ($okCons) { break }
            $msgCons = [string](Get-Prop (Get-Prop $consResp "erro") "mensagem")
            if ($msgCons -and $msgCons -notmatch "Nenhum DF-e localizado|sem detalhes|atrasar") {
                break
            }
        }
        if ($okCons) {
            Write-SmokeLog -Step "$NomeModelo consultar" -Status "OK" `
                -Detail "cStat=$(Get-Prop $consResp 'codigoStatus')" -Data $consResp
        }
        else {
            $msgCons = [string](Get-Prop (Get-Prop $consResp "erro") "mensagem")
            $statusCons = if ($msgCons -match "Nenhum DF-e localizado|atrasar") { "WARN" } else { "FAIL" }
            Write-SmokeLog -Step "$NomeModelo consultar" -Status $statusCons `
                -Detail $(if ($msgCons) { $msgCons } else { "cStat=$(Get-Prop $consResp 'codigoStatus')" }) `
                -Data $consResp
        }
    }

    Start-Sleep -Seconds 2

    $cancelBody = & $MontarCancelamento $chave $protocolo
    $cancelResp = Invoke-FiscalApi -Method POST -Path "/api/$($NomeModelo.ToLower())/cancelar" -Body $cancelBody
    # NF-e/NFC-e/NFS-e: cancelamento homologado costuma retornar cStat 135
    if (Test-RespostaAutorizada $cancelResp -CodigosOk @("100", "135")) {
        Write-SmokeLog -Step "$NomeModelo cancelar" -Status "OK" -Detail "cStat=$(Get-Prop $cancelResp 'codigoStatus')" -Data $cancelResp
    }
    else {
        $erro = Get-Prop $cancelResp "erro"
        $det = if ($erro) { "$(Get-Prop $erro 'tipo'): $(Get-Prop $erro 'mensagem')" } else { "falha no cancelamento" }
        Write-SmokeLog -Step "$NomeModelo cancelar" -Status "FAIL" -Detail $det -Data $cancelResp
        Write-SmokeLog -Step "$NomeModelo AVISO" -Status "WARN" -Detail "Nota autorizada mas NAO cancelada. Chave: $chave"
    }
}

# -- Inicio -------------------------------------------------------------------
Write-Host ""
Write-Host "=== Smoke PRODUCAO - $BaseUrl ===" -ForegroundColor Red
Write-Host "Emitente: $Cnpj | Modelo: $Modelo | Confirmar: $ConfirmarProducao" -ForegroundColor DarkGray
Write-Host "Cliente teste: SDBR SOLUCOES DIGITAIS LTDA (53.658.565/0001-27)" -ForegroundColor DarkGray
Write-Host "Evidencias: $EvidenceFile" -ForegroundColor DarkGray
Write-Host ""

if (-not $ConfirmarProducao) {
    Write-SmokeLog -Step "Modo seguro" -Status "WARN" -Detail "Sem -ConfirmarProducao: apenas pre-checks (sem emissao)"
}

# 1. Health
$health = Invoke-FiscalApi -Method GET -Path "/health" -SemApiKey
if ($health.status -match "healthy|degraded") {
    $healthDetail = "status=$($health.status) banco=$($health.banco) cert=$($health.checks.certificados_emitentes)"
    Write-SmokeLog -Step "GET /health" -Status "OK" -Detail $healthDetail -Data $health
}
else {
    Write-SmokeLog -Step "GET /health" -Status "FAIL" -Detail "status=$($health.status)" -Data $health
}

# 2. Auth
if (-not $DryRun) {
    try {
        Invoke-WebRequest -Method GET -Uri "$BaseUrl/api/nfe/status-sefaz" -UseBasicParsing | Out-Null
        Write-SmokeLog -Step "Auth sem API Key" -Status "FAIL" -Detail "esperava 401"
    }
    catch {
        $code = [int]$_.Exception.Response.StatusCode
        if ($code -eq 401) {
            Write-SmokeLog -Step "Auth sem API Key" -Status "OK" -Detail "HTTP 401"
        }
        else {
            Write-SmokeLog -Step "Auth sem API Key" -Status "FAIL" -Detail "HTTP $code"
        }
    }
}

# 3. Emitente cadastrado
$emitente = Invoke-FiscalApi -Method GET -Path "/api/emitentes/$Cnpj"
if ($emitente -and $emitente.cnpj) {
    $amb = if ($emitente.PSObject.Properties.Name -contains "ambiente") { $emitente.ambiente } else { "(n/a)" }
    $certCheck = if ($health.checks) { $health.checks.certificados_emitentes } else { "unknown" }
    $possuiCsc = if ($emitente.PSObject.Properties.Name -contains "possuiCscProducao") { $emitente.possuiCscProducao } else { $false }
    Write-SmokeLog -Step "GET /api/emitentes/{cnpj}" -Status "OK" `
        -Detail "razao=$($emitente.razaoSocial) ambiente=$amb cert=$certCheck" -Data @{
            cnpj = $emitente.cnpj
            ambiente = $amb
            possuiCscProducao = $possuiCsc
        }
    if ($amb -ne "Producao" -and $amb -ne "(n/a)") {
        Write-SmokeLog -Step "Ambiente emitente" -Status "WARN" -Detail "Emitente cadastrado como '$amb' (esperado Producao)"
    }
    if ($certCheck -ne "healthy") {
        Write-SmokeLog -Step "Certificado emitente" -Status "WARN" -Detail "health certificados_emitentes=$certCheck"
    }
}
else {
    Write-SmokeLog -Step "GET /api/emitentes/{cnpj}" -Status "FAIL" -Detail "emitente nao encontrado"
}

# 4. Numeração
$listaNum = Invoke-FiscalApi -Method GET -Path "/api/numeracao" -Query @{
    cnpj     = $Cnpj
    ambiente = "Producao"
}
if ($listaNum -and $listaNum.itens) {
    Write-SmokeLog -Step "GET /api/numeracao" -Status "OK" -Detail "total=$($listaNum.total) series" -Data $listaNum
    foreach ($item in $listaNum.itens) {
        Write-Host "  $($item.modeloDescricao) serie $($item.serie): ultimo=$($item.ultimoNumero) proximo=$($item.proximoNumero)" -ForegroundColor DarkGray
    }
}
else {
    Write-SmokeLog -Step "GET /api/numeracao" -Status "SKIP" -Detail "nenhuma serie cadastrada"
}

# 5. Emissão + cancelamento (somente com confirmação)
if (-not $ConfirmarProducao) {
    Write-Host ""
    Write-Host "Para emitir e cancelar em producao, execute:" -ForegroundColor Yellow
    Write-Host "  .\scripts\smoke-producao.ps1 -ConfirmarProducao" -ForegroundColor White
    Write-Host ""
}
else {
    Write-SmokeLog -Step "Emissao producao" -Status "WARN" -Detail "Iniciando emissao REAL com cancelamento"

    $modelos = if ($Modelo -eq "Todos") { @("NFe", "NFCe", "NFSe") } else { @($Modelo) }

    foreach ($m in $modelos) {
        switch ($m) {
            "NFe" {
                Test-CicloEmitirCancelar `
                    -NomeModelo "nfe" `
                    -ArquivoEmitir "nfe-emitir-sdbr.json" `
                    -AjustarEmitir {
                        param($b)
                        $b.serie = $SerieNFe
                        $b.numeroNota = Get-ProximoNumero -ModeloCodigo "55" -Serie $SerieNFe
                    } `
                    -PathConsultar "/api/nfe/consultar" `
                    -MontarConsulta {
                        param($chave)
                        return @{ emitenteCnpj = $Cnpj; chaveAcesso = $chave }
                    } `
                    -MontarCancelamento {
                        param($chave, $protocolo)
                        return @{
                            emitenteCnpj  = $Cnpj
                            chaveAcesso   = $chave
                            protocolo     = $protocolo
                            justificativa = $JustificativaCancelamento
                        }
                    }
            }
            "NFCe" {
                Test-CicloEmitirCancelar `
                    -NomeModelo "nfce" `
                    -ArquivoEmitir "nfce-emitir-sdbr.json" `
                    -AjustarEmitir {
                        param($b)
                        $b.serie = $SerieNFCe
                        $b.numeroNota = Get-ProximoNumero -ModeloCodigo "65" -Serie $SerieNFCe
                        if ($IdCsc) { $b.idCsc = $IdCsc }
                        if ($Csc) { $b.csc = $Csc }
                    } `
                    -MontarCancelamento {
                        param($chave, $protocolo)
                        return @{
                            emitenteCnpj  = $Cnpj
                            chaveAcesso   = $chave
                            protocolo     = $protocolo
                            justificativa = $JustificativaCancelamento
                        }
                    }
            }
            "NFSe" {
                Test-CicloEmitirCancelar `
                    -NomeModelo "nfse" `
                    -ArquivoEmitir "nfse-emitir-sdbr.json" `
                    -AjustarEmitir {
                        param($b)
                        $b.serie = $SerieNFSe
                        $b.competencia = (Get-Date).ToString("yyyy-MM-dd")
                    } `
                    -PathConsultar "/api/nfse/consultar" `
                    -MontarConsulta {
                        param($chave)
                        return @{ emitenteCnpj = $Cnpj; chaveAcesso = $chave }
                    } `
                    -MontarCancelamento {
                        param($chave, $protocolo)
                        return @{
                            emitenteCnpj     = $Cnpj
                            chaveAcesso      = $chave
                            codigoMotivo     = "ErroEmissao"
                            descricaoMotivo  = $NfseMotivoCancelamento
                        }
                    }
            }
        }
    }
}

# Resumo
Write-Host ""
Write-Host "=== Resumo: OK=$($script:Pass) FAIL=$($script:Fail) SKIP=$($script:Skip) ===" -ForegroundColor White
Write-Host "Evidencias: $EvidenceFile" -ForegroundColor DarkGray

if ($script:Fail -gt 0) { exit 1 }
exit 0
