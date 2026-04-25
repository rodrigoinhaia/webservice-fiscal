# FiscalService — WebService REST para Emissão Fiscal Brasileira

Microsserviço em **ASP.NET Core 8** para emissão de documentos fiscais eletrônicos brasileiros via REST API. Utiliza a biblioteca [DFe.NET (ZeusAutomacao)](https://github.com/ZeusAutomacao/DFe.NET) para comunicação com a SEFAZ.

Compatível com **Docker/Linux** — sem dependência de Windows ou interface gráfica.

---

## Documentos Suportados

| Documento | Modelo | Versão | Operações |
|-----------|--------|--------|-----------|
| NF-e | 55 | 4.0 | Emitir, Cancelar, CC-e, Consultar, Inutilizar, Status SEFAZ |
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
- **Dockerfile:** `src/FiscalService.Api/Dockerfile`.
- **Porta do container:** `8080` (mapeie a porta pública do painel para `8080`).
- **Variáveis de ambiente (mínimo):** `ApiKey`, `Database__ConnectionString` (Postgres gerenciado ou URL interna do painel). Opcionalmente `FISCAL__Ambiente`, `FISCAL__TimeoutWs`, etc., como no `docker-compose.yml`.
- **Persistência:** monte volumes ou disco persistente em `/app/xmls`, `/app/certificados` e `/app/logs` se quiser reter XMLs, certificados e logs entre deploys.

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
  "schemas": "ok"
}
```

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
| `DB_PASSWORD` | Sim* | Senha Npgsql. Se `Database__ConnectionString` **não** existir, monta `Host=localhost;Port=5432;Database=fiscal_db;Username=fiscal_user;Password=…`. |
| `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER` | Não | Ajustam a montagem local da connection string (padrão: `localhost`, `5432`, `fiscal_db`, `fiscal_user`). |
| `Database__ConnectionString` | Não | Se definida, **substitui** a montagem por `DB_PASSWORD` (útil para URLs/host diferentes). |
| `SERVICE_PORT` | Não | Usada pelo **Docker Compose** para publicar a API (`${SERVICE_PORT:-5555}`). |
| `FISCAL_AMBIENTE` | Não | Mapeada para `Fiscal__Ambiente` (`Homologacao` ou `Producao`). |
| `FISCAL_TIMEOUT_WS` | Não | Mapeada para `Fiscal__TimeoutWs` (segundos). |

\*Obrigatória para a API aceitar chamadas autenticadas e para conectar ao banco com o template padrão.

Detalhes e exemplos: `.env.example`.

---

## Autenticação

Todos os endpoints (exceto `/health`) exigem o header:

```
X-Api-Key: <sua-chave-configurada-em-API_KEY>
```

Sem o header ou com chave inválida, retorna `401 Unauthorized`.

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

### DANFE / PDF

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/danfe/nfe` | Gera PDF do DANFE NF-e (base64) |
| POST | `/api/danfe/nfce` | Gera PDF do DANFE NFC-e/cupom (base64) |

### Certificado

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/certificado/validar` | Valida um .pfx e retorna informações |
| POST | `/api/certificado/upload` | Faz upload de um certificado .pfx |

### Numeração

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/api/numeracao/{cnpj}/{modelo}/{serie}` | Próximo número disponível |
| POST | `/api/numeracao/confirmar` | Confirma uso de um número |

### Geral

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/consulta/status-servico` | Status do serviço SEFAZ |
| GET | `/health` | Health check (sem autenticação) |

---

## Exemplo de Chamada — Emissão NF-e

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
    "TimeoutWs": 30
  },
  "Database": {
    "ConnectionString": "Host=...;Database=fiscal_db;Username=...;Password=..."
  }
}
```

### Variáveis de ambiente (Docker Compose)

O `docker-compose.yml` lê o `.env` da raiz e injeta no container (formato ASP.NET `__`):

| Origem no `.env` | Variável no container | Descrição |
|------------------|------------------------|-----------|
| `API_KEY` | `ApiKey` | Chave do header `X-Api-Key` |
| `DB_PASSWORD` | trecho `Password=` em `Database__ConnectionString` | Senha do Postgres (`Host=db;…`) |
| `FISCAL_AMBIENTE` | `Fiscal__Ambiente` | `Homologacao` ou `Producao` |
| `FISCAL_TIMEOUT_WS` | `Fiscal__TimeoutWs` | Timeout SEFAZ (segundos) |
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
- **DANFE**: no Linux o endpoint responde como não suportado até integrar um gerador multiplataforma (o pacote nativo histórico é voltado a Windows).
- **Reforma Tributária**: manter o pacote `Zeus.Net.NFe.NFCe` sempre na versão mais recente.

---

## Referências

- [DFe.NET (ZeusAutomacao)](https://github.com/ZeusAutomacao/DFe.NET)
- [Portal NF-e SEFAZ](http://www.nfe.fazenda.gov.br)
- [Unimake DFe](https://github.com/Unimake/DFe) — referência de implementação REST
