# FiscalService — WebService REST para Emissão Fiscal Brasileira

Microsserviço em **ASP.NET Core 8** para emissão de documentos fiscais eletrônicos brasileiros via REST API. Utiliza a biblioteca [DFe.NET (ZeusAutomacao)](https://github.com/ZeusAutomacao/DFe.NET) para comunicação com a SEFAZ.

Compatível com **Docker/Linux** — sem dependência de Windows ou interface gráfica.

> **Visão completa de capacidades:** [`docs/CAPACIDADES.md`](docs/CAPACIDADES.md)
> · Plano: [`PLANNING.md`](PLANNING.md) · Status: [`PROGRESS.md`](PROGRESS.md)
> · Smoke test: [`docs/SMOKE-HOMOLOGACAO.md`](docs/SMOKE-HOMOLOGACAO.md)
> · Estratégia DANFE: [`docs/DANFE-ESTRATEGIA.md`](docs/DANFE-ESTRATEGIA.md)

---

## Documentos Suportados

| Documento | Modelo | Versão | Operações |
|-----------|--------|--------|-----------|
| NF-e | 55 | 4.0 | Emitir (contingência), Cancelar, CC-e, Consultar, Inutilizar, Distribuição DF-e, Manifestação destinatário, Status SEFAZ |
| NFC-e | 65 | 4.0 | Emitir (CSC/IdCSC), Cancelar |
| CT-e | 57 | 4.0 | Emitir, Cancelar |
| MDF-e | 58 | 3.0 | Emitir, Encerrar, Cancelar |

---

## Pré-requisitos

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://docs.docker.com/get-docker/) + Docker Compose
- PostgreSQL 16 (ou via Docker Compose)
- Certificado digital A1 (arquivo `.pfx`) do emitente
- Schemas XSD do DFe.NET (ver seção abaixo)

---

## Início Rápido

### 1. Configurar variáveis de ambiente

```bash
cp .env.example .env
# Edite .env com valores reais:
# API_KEY, DB_PASSWORD, FISCAL_AMBIENTE
```

### 2. Baixar schemas XSD do DFe.NET

Os schemas são obrigatórios para validação dos XMLs:

```bash
git clone --depth 1 https://github.com/ZeusAutomacao/DFe.NET.git /tmp/dfe
cp -r /tmp/dfe/NFe.AppTeste/Schemas/* src/FiscalService.Api/Schemas/
```

### 3. Subir com Docker Compose

```bash
docker compose up --build -d
```

O serviço ficará disponível em `http://localhost:5555`.

### Easypanel (ou outro painel Docker)

- **Contexto de build:** raiz do repositório (onde está `src/`).
- **Dockerfile:** `Dockerfile` na raiz do repositório (contexto de build = raiz).
- **Porta do container:** `8080` (mapeie a porta pública do painel para `8080`).
- **Variáveis de ambiente (mínimo):** `ApiKey`, `Database__ConnectionString` (Postgres gerenciado ou URL interna do painel). Opcionalmente `FISCAL__Ambiente`, `FISCAL__TimeoutWs`, etc., como no `docker-compose.yml`.
- **Persistência:** monte volumes ou disco persistente em `/app/xmls`, `/app/certificados` e `/app/logs` se quiser reter XMLs, certificados e logs entre deploys. Sem volume em `/app/logs`, o app continua no **console** (sink de arquivo é ignorado se o diretório não for gravável). Configure **backup** do volume do Postgres no painel (snapshot ou dump agendado).
- **Checklist pós-deploy:** ver [docs/SMOKE-HOMOLOGACAO.md](docs/SMOKE-HOMOLOGACAO.md) ou execute `.\scripts\smoke-homologacao.ps1` (PowerShell 5.1+).

### 4. Verificar saúde

```bash
curl http://localhost:5555/health
```

Resposta esperada:
```json
{
  "status": "healthy",
  "versao": "1.0.0",
  "timestamp": "2025-04-24T10:00:00Z",
  "banco": "healthy",
  "certificados": "healthy",
  "schemas": "ok",
  "checks": {
    "postgresql": "healthy",
    "certificados_emitentes": "healthy"
  }
}
```

`certificados_emitentes` fica **degraded** se algum PFX de emitente ativo vence em menos de `Fiscal:DiasAlertaCertificado` dias (padrão 30), ou **unhealthy** se expirado/ausente.

---

## CI e validação de entrada

No GitHub Actions (`.github/workflows/ci.yml`): `dotnet restore`, `dotnet build` e `dotnet test` na solução, mais `docker build -f Dockerfile .` na raiz do repositório. O Swagger em Development lista respostas comuns (400, 401, 422, 429) em cada operação.

Além das **DataAnnotations** nos DTOs, a API usa **FluentValidation** (`FiscalService.Api.Validation`): regras adicionais são aplicadas automaticamente e entram no `ModelState` (respostas `400` com detalhes). Testes unitários dos validadores: `tests/FiscalService.Api.Tests`.

**Integração:** `tests/FiscalService.Api.IntegrationTests` sobe PostgreSQL com **Testcontainers**, aplica `MigrateAsync` e cobre `NumeracaoService`. Se `docker info` não estiver disponível na máquina, esses testes são **ignorados** (SkippableFact); no CI (Ubuntu + Docker) eles devem executar de fato.

---

## Desenvolvimento Local

1. Copie e preencha o `.env` na **raiz do repositório** (mesmo nível do `docker-compose.yml`). O `Program.cs` carrega esse arquivo **antes** do host (`EnvBootstrap` + pacote **DotNetEnv**), assim `dotnet run` usa as mesmas variáveis que o Compose (`API_KEY`, `DB_PASSWORD`, etc.).
2. Suba o PostgreSQL (ex.: `docker compose up -d db` ou uma instância local na porta 5432).
3. Na primeira execução, o app aplica **migrations automaticamente** (`MigrateAsync` no startup). Só é necessário `dotnet ef database update` se quiser atualizar o schema **sem** subir a API.

```bash
# Na raiz do repo (recomendado — o .env é encontrado subindo a partir do cwd)
dotnet run --project src/FiscalService.Api/FiscalService.Api.csproj

# Ou, a partir da pasta do projeto (o bootstrap também sobe pastas até achar o .env)
cd src/FiscalService.Api
dotnet restore
dotnet run
```

**Ferramenta EF (opcional):** `dotnet tool install --global dotnet-ef --version 8.0.*` — para criar novas migrações (`dotnet ef migrations add ...`).

A URL de escuta segue `launchSettings.json` / `ASPNETCORE_URLS` (ex.: `https://localhost:7xxx` em Development).

### Variáveis do arquivo `.env` (raiz do repo)

| Variável | Obrigatória | Descrição |
|----------|-------------|-----------|
| `API_KEY` | Sim* | Valor do header `X-Api-Key`. É copiada para a configuração `ApiKey` no startup. |
| `API_KEY_PREVIOUS` | Não | Durante rotação: mesclada com `ApiKey` no startup para aceitar chave nova e antiga. |
| `RateLimiting__Enabled` | Não | `false` desliga o limite global por IP (padrão: ligado). |
| `RateLimiting__PermitLimit` | Não | Máximo de requisições por IP por janela (padrão: 180). |
| `RateLimiting__WindowSeconds` | Não | Duração da janela em segundos (padrão: 60). |
| `DATABASE_URL` | Não | URL `postgres://` ou `postgresql://` — convertida para `Database__ConnectionString` (Npgsql). Tem precedência sobre a montagem por senha. |
| `DB_PASSWORD` | Sim* | Senha do usuário Postgres **ou** URL `postgres://` (legado): se for URL, o app converte. Se for só senha, monta `Host=localhost;…` com `DB_*`. |
| `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER` | Não | Ajustam a montagem local quando `DB_PASSWORD` **não** é URL (padrão: `localhost`, `5432`, `fiscal_db`, `fiscal_user`). |
| `Database__ConnectionString` | Não | Se definida, **substitui** toda montagem automática (formato parâmetros Npgsql). |
| `SERVICE_PORT` | Não | Usada pelo **Docker Compose** para publicar a API (`${SERVICE_PORT:-5555}`). |
| `FISCAL_AMBIENTE` | Não | Mapeada para `Fiscal__Ambiente` (`Homologacao` ou `Producao`). |
| `FISCAL_TIMEOUT_WS` | Não | Mapeada para `Fiscal__TimeoutWs` (segundos). |
| `Fiscal__DiasAlertaCertificado` | Não | Alerta de vencimento no `/health` (dias; padrão `30`). |
| `Fiscal__SefazRetryHabilitado` | Não | Retry em falha transitória SEFAZ (padrão `true`). |
| `Fiscal__SefazRetryMaxTentativas` | Não | Tentativas incluindo a 1ª (padrão `3`). |
| `Fiscal__SefazRetryIntervaloMs` | Não | Intervalo base entre retentativas em ms (padrão `1000`). |
| `Serilog__File__Disabled` | Não | `true` força apenas console (sem tentar arquivo). |
| `Serilog__File__Path` | Não | Sobrescreve `Serilog:File:Path` (caminho do log em disco). |
| `OpenTelemetry__Enabled` | Não | `true` liga exportação OTLP (exige endpoint). |
| `OpenTelemetry__OtlpEndpoint` | Não | URL absoluta do coletor (ex.: `http://localhost:4317`). Alternativa: `OTEL_EXPORTER_OTLP_ENDPOINT`. |

\*Obrigatória para a API aceitar chamadas autenticadas e para conectar ao banco com o template padrão.

Detalhes e exemplos: `.env.example`. Checklist de homologação: [docs/SMOKE-HOMOLOGACAO.md](docs/SMOKE-HOMOLOGACAO.md).

---

## Cadastro de emitentes (certificado persistido)

Cadastre o emitente **uma vez**; nas emissões use só `emitenteCnpj` (sem reenviar senha do `.pfx`).

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/emitentes` | Cadastra emitente + valida certificado (opcional CNPJ × cert) |
| GET | `/api/emitentes/{cnpj}` | Consulta cadastro (sem senha) |
| GET | `/api/emitentes` | Lista paginada |
| PUT | `/api/emitentes/{cnpj}` | Atualiza dados ou certificado |
| DELETE | `/api/emitentes/{cnpj}` | Desativa emitente |

Exemplos: [`docs/exemplos/emitente/`](docs/exemplos/emitente/) e [`docs/exemplos/nfe/emitir-via-emitente-cnpj.json`](docs/exemplos/nfe/emitir-via-emitente-cnpj.json).

A senha do certificado é armazenada **criptografada** (`IDataProtection`). Em produção, proteja as chaves do Data Protection (volume persistente ou Key Vault).

---

## Autenticação

Todos os endpoints (exceto `/health`) exigem o header:

```
X-Api-Key: <sua-chave-configurada-em-API_KEY>
```

Sem o header ou com chave inválida, retorna `401 Unauthorized`.

**Rotação de chave:** em `ApiKey` (ou `API_KEY`) você pode informar **várias** chaves separadas por vírgula, pipe ou ponto-e-vírgula; qualquer uma é aceita. Opcionalmente use `API_KEY_PREVIOUS` no `.env` durante a troca: o bootstrap concatena com a chave atual para não derrubar clientes que ainda enviam a chave antiga.

**Limite de taxa:** por padrão há um *rate limit* global por endereço IP (janela fixa; `/health` não conta). Em caso de excesso, resposta `429` com JSON `erro.tipo = LimiteExcedido`. Ajuste em `RateLimiting` no `appsettings.json` ou via variáveis `RateLimiting__*`.

---

## Tributação (ICMS / Simples Nacional / Lucro Presumido e Real)

`configuracaoEmitente.crt`: **1 ou 2** = Simples Nacional, **3** = regime normal (**Lucro Presumido e Lucro Real** usam CRT 3; o ERP envia CST e valores).

- **CRT 3:** `cstIcms` — `00`, `10`, `20`, `30`, `40`, `41`, `50`, `51`, `60`, `70`, `90` (CST não suportado → **400**, sem fallback silencioso).
- **CRT 1 ou 2:** `csosnIcms` — `101`, `102`, `103`, `201`, `202`, `203`, `500`, `900` (padrão `102`).
- **IPI (opcional):** `cstIpi` + `valorIpi` / `aliquotaIpi` — `IPITrib` ou `IPINT`.
- **PIS/COFINS:** `01`/`02` alíquota; `03` quantidade (`PISQtde`/`COFINSQtde`); `04`–`09` NT; `49`/`99` outros; default `07`.
- **DIFAL (opcional):** `baseCalculoUfDest`, percentuais e valores → `ICMSUFDest`.

Matriz completa: [`docs/TRIBUTACAO-MATRIZ.md`](docs/TRIBUTACAO-MATRIZ.md) · Guia regimes: [`docs/GUIA-REGIMES.md`](docs/GUIA-REGIMES.md) · Exemplos JSON: [`docs/exemplos/`](docs/exemplos/) · Schemas XSD: [`docs/SCHEMAS-DFE.md`](docs/SCHEMAS-DFE.md) · Roadmap: [`docs/ROADMAP-TRIBUTACAO-REGIMES.md`](docs/ROADMAP-TRIBUTACAO-REGIMES.md).

**Contingência:** em `POST /api/nfe/emitir` use `tipoEmissao`: `Normal`, `SVC-AN`, `SVC-RS` ou `Offline`, com `dataHoraContingencia` e `justificativaContingencia` (mín. 15 caracteres) quando não for normal. Exemplo: [`docs/exemplos/nfe/contingencia-svc-an.json`](docs/exemplos/nfe/contingencia-svc-an.json).

**Retry SEFAZ:** falhas transitórias (timeout/rede) são reexecutadas conforme `Fiscal:SefazRetry*` — não reenvia automaticamente em rejeição de negócio (`cStat`).

**Distribuição DF-e / manifestação:** ver rotas NF-e abaixo; exemplos em [`docs/exemplos/nfe/distribuicao-dfe.json`](docs/exemplos/nfe/distribuicao-dfe.json) e [`manifestar-ciencia.json`](docs/exemplos/nfe/manifestar-ciencia.json).

**Swagger (Development):** exemplos JSON embutidos nas rotas principais via `OpenApiJsonExamplesFilter`.

---

## Observabilidade (OpenTelemetry)

Com `OpenTelemetry:Enabled=true` **ou** `OTEL_EXPORTER_OTLP_ENDPOINT` definida, e um endpoint OTLP absoluto (`OpenTelemetry:OtlpEndpoint` ou env), a API exporta **traces** (ASP.NET Core + `HttpClient`) e **métricas**, incluindo o contador `fiscal.sefaz.outcomes` (tags `operation`, `sucesso`, `cstat`) alimentado automaticamente nas respostas `FiscalResponse`.

---

## Endpoints da API

### NF-e

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/nfe/emitir` | Emite uma NF-e 4.0 |
| POST | `/api/nfe/cancelar` | Cancela uma NF-e autorizada |
| POST | `/api/nfe/carta-correcao` | Emite CC-e (Carta de Correção Eletrônica) |
| POST | `/api/nfe/consultar` | Consulta situação na SEFAZ |
| POST | `/api/nfe/inutilizar` | Inutiliza faixa de numeração |
| POST | `/api/nfe/distribuicao-dfe` | Distribuição DF-e (NSU / chave) — documentos do destinatário |
| POST | `/api/nfe/manifestar-destinatario` | Manifestação (ciência, confirmação, desconhecimento, não realizada) |
| GET | `/api/nfe/status-sefaz` | Consulta status do serviço SEFAZ |

### NFC-e

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/nfce/emitir` | Emite uma NFC-e 4.0 |
| POST | `/api/nfce/cancelar` | Cancela uma NFC-e |

### CT-e

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/cte/emitir` | Emite um CT-e 4.0 |
| POST | `/api/cte/cancelar` | Cancela um CT-e |

### MDF-e

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/mdfe/emitir` | Emite um MDF-e 3.0 |
| POST | `/api/mdfe/encerrar` | Encerra um MDF-e |
| POST | `/api/mdfe/cancelar` | Cancela um MDF-e |

### DANFE / PDF e HTML

Contrato e opções: [docs/DANFE-ESTRATEGIA.md](docs/DANFE-ESTRATEGIA.md). **PDF** em Linux ainda retorna `NaoSuportado`; **HTML** funciona em qualquer SO (impressão / PDF pelo navegador).

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/danfe/nfe` | Gera PDF do DANFE NF-e (base64) — Linux: `NaoSuportado` |
| POST | `/api/danfe/nfe/html` | Gera HTML do DANFE NF-e (`html` no JSON ou `?inline=true` → `text/html`) |
| POST | `/api/danfe/nfce` | Gera PDF do cupom NFC-e (base64) — Linux: `NaoSuportado` |
| POST | `/api/danfe/nfce/html` | Gera HTML da NFC-e (mesmo corpo que a rota PDF) |

Exemplo HTML direto no navegador (corpo mínimo; ajuste o XML):

```bash
curl -sS -X POST "http://localhost:5555/api/danfe/nfe/html?inline=true" \
  -H "X-Api-Key: sua-chave-api" \
  -H "Content-Type: application/json" \
  -d "{\"xmlNfeProc\":\"<?xml version=\\\"1.0\\\"?>...nfeProc...\"}" \
  -o danfe.html
```

### Certificado

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/certificado/validar` | Valida um .pfx e retorna informações |
| POST | `/api/certificado/upload` | Upload JSON com `.pfx` em Base64 |
| POST | `/api/certificado/upload-arquivo` | Upload multipart: arquivo `.pfx`/`.p12` + senha (opcional `nome`) |

Exemplo (multipart, sem Base64 no cliente):

```bash
curl -X POST http://localhost:5555/api/certificado/upload-arquivo \
  -H "X-Api-Key: sua-chave-api" \
  -F "arquivo=@/caminho/para/empresa.pfx" \
  -F "senha=senha_do_pfx" \
  -F "nome=empresa.pfx"
```

O campo `nome` é opcional; se omitir, usa o nome do arquivo enviado. A resposta em sucesso é a mesma do upload JSON (`pathRelativo`, `pathAbsoluto`).

### Numeração

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/api/numeracao/{cnpj}/{modelo}/{serie}` | Próximo número disponível |
| POST | `/api/numeracao/confirmar` | Confirma uso de um número |

### Geral

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/consulta/status-servico` | Status do serviço SEFAZ (NF-e, NFC-e, CT-e, MDF-e) |
| GET | `/api/emissoes` | Histórico paginado de emissões |
| GET | `/api/emissoes/{chave}` | Último log por chave de acesso |
| GET | `/health` | Health check (sem autenticação) |

---

## Exemplo de Chamada — Emissão NF-e

Payloads por regime (Simples, LP, LR): pasta [`docs/exemplos/nfe/`](docs/exemplos/nfe/). Com emitente cadastrado, prefira só `emitenteCnpj` — ver [`emitir-via-emitente-cnpj.json`](docs/exemplos/nfe/emitir-via-emitente-cnpj.json).

```bash
curl -X POST http://localhost:5555/api/nfe/emitir \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: sua-chave-api" \
  -d '{
    "configuracaoEmitente": {
      "cnpj": "00000000000000",
      "razaoSocial": "Empresa Teste LTDA",
      "ie": "000000000",
      "crt": 1,
      "uf": "RS",
      "ambiente": "Homologacao",
      "certificadoPath": "empresa.pfx",
      "certificadoSenha": "senha123"
    },
    "numeroNota": 1,
    "serie": "1",
    "naturezaOperacao": "Venda de Mercadoria",
    "destinatario": {
      "cpf": "00000000000",
      "razaoSocial": "Consumidor Final",
      "indicadorIe": 9
    },
    "itens": [
      {
        "numeroItem": 1,
        "codigoProduto": "001",
        "descricaoProduto": "Produto Teste",
        "ncm": "00000000",
        "cfop": "5102",
        "unidadeComercial": "UN",
        "quantidadeComercial": 1.0,
        "valorUnitarioComercial": 10.00,
        "valorTotalBruto": 10.00,
        "cstIcms": "00",
        "aliquotaIcms": 12.0,
        "valorIcms": 1.20
      }
    ],
    "pagamentos": [
      {
        "formaPagamento": "01",
        "valorPagamento": 10.00
      }
    ]
  }'
```

**Resposta de sucesso:**
```json
{
  "sucesso": true,
  "chaveAcesso": "35250100000000000000550010000000011000000019",
  "protocolo": "135250000000001",
  "codigoStatus": "100",
  "mensagem": "Autorizado o uso da NF-e",
  "xmlAutorizado": "<nfeProc>...</nfeProc>",
  "danfePdfBase64": "JVBERi0x..."
}
```

**Resposta de erro:**
```json
{
  "sucesso": false,
  "erro": {
    "tipo": "RejeicaoSefaz",
    "mensagem": "Rejeição SEFAZ: CNPJ do emitente inválido",
    "detalhe": "cStat: 539",
    "timestamp": "2025-04-24T10:00:00Z"
  }
}
```

---

## Configuração

### appsettings.json

```json
{
  "ApiKey": "DEFINIR_VIA_ENV",
  "Fiscal": {
    "Ambiente": "Homologacao",
    "SalvarXmls": true,
    "DiretorioXmls": "/app/xmls",
    "DiretorioSchemas": "/app/schemas",
    "DiretorioCertificados": "/app/certificados",
    "TimeoutWs": 30,
    "DiasAlertaCertificado": 30,
    "SefazRetryHabilitado": true,
    "SefazRetryMaxTentativas": 3,
    "SefazRetryIntervaloMs": 1000
  },
  "Database": {
    "ConnectionString": "Host=...;Database=fiscal_db;Username=...;Password=..."
  },
  "Serilog": {
    "WriteTo": [ { "Name": "Console" } ],
    "File": {
      "Path": "/app/logs/fiscal-.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 30,
      "Disabled": false
    }
  }
}
```

Logs em arquivo: o trecho `Serilog:File` só vira sink se o diretório existir e for gravável; senão o app segue só com **Console** (sem derrubar o host). Para forçar só console, use `Serilog__File__Disabled=true` no ambiente.

O `docker-compose.yml` lê o `.env` da raiz e injeta no container (formato ASP.NET `__`):

| Origem no `.env` | Variável no container | Descrição |
|------------------|------------------------|-----------|
| `API_KEY` | `ApiKey` | Chave do header `X-Api-Key` |
| `DB_PASSWORD` | trecho `Password=` em `Database__ConnectionString` | Senha do Postgres (`Host=db;…`) |
| `FISCAL_AMBIENTE` | `Fiscal__Ambiente` | `Homologacao` ou `Producao` |
| `FISCAL_TIMEOUT_WS` | `Fiscal__TimeoutWs` | Timeout SEFAZ (segundos) |
| `Fiscal__DiasAlertaCertificado` | idem | Alerta certificado no `/health` |
| `Fiscal__SefazRetry*` | idem | Retry em falha transitória SEFAZ |
| `SERVICE_PORT` | mapeamento de porta host | Padrão `5555` |

Você também pode definir `Database__ConnectionString` completa no compose, se preferir.

---

## Volumes Docker

| Volume | Caminho no Container | Conteúdo |
|--------|---------------------|----------|
| `fiscal_xmls` | `/app/xmls` | XMLs autorizados (documentos fiscais legais) |
| `fiscal_certs` | `/app/certificados` | Certificados .pfx |
| `fiscal_logs` | `/app/logs` | Logs rotativos do Serilog |
| `fiscal_pgdata` | `/var/lib/postgresql/data` | Dados do PostgreSQL |

---

## Avisos Importantes

- **Certificados A1**: nunca versionar arquivos `.pfx`. Use o endpoint de upload ou monte como volume.
- **XMLs autorizados**: são documentos fiscais legais. Os volumes devem ter backup adequado.
- **Thread-safety**: os serviços de emissão são instanciados como `Transient` porque o DFe.NET não é thread-safe.
- **Numeração**: o `NumeracaoService` usa `SELECT FOR UPDATE` (PostgreSQL) para garantir atomicidade.
- **DANFE**: no Linux o endpoint pode responder `NaoSuportado` até integrar um gerador PDF multiplataforma — ver [docs/DANFE-ESTRATEGIA.md](docs/DANFE-ESTRATEGIA.md).
- **Reforma Tributária**: manter o pacote `Zeus.Net.NFe.NFCe` sempre na versão mais recente.

---

## Referências

- [DFe.NET (ZeusAutomacao)](https://github.com/ZeusAutomacao/DFe.NET)
- [Portal NF-e SEFAZ](http://www.nfe.fazenda.gov.br)
- [Unimake DFe](https://github.com/Unimake/DFe) — referência de implementação REST
