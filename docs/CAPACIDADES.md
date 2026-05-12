# FiscalService — Catálogo de Capacidades

Documento de referência detalhando **todas as capacidades** do `FiscalService.Api`
(microsserviço REST em **ASP.NET Core 8** para emissão de documentos fiscais
eletrônicos brasileiros via [DFe.NET / ZeusAutomacao](https://github.com/ZeusAutomacao/DFe.NET)).

> Para visão de produto / requisitos / fases: `PLANNING.md`. Para status de execução: `PROGRESS.md`.
> Para guia de uso operacional: `README.md`. Este arquivo é o **mapa funcional e técnico** do que existe hoje.

---

## Sumário

1. [Visão Geral](#1-visão-geral)
2. [Arquitetura e Camadas](#2-arquitetura-e-camadas)
3. [Documentos Fiscais Suportados](#3-documentos-fiscais-suportados)
4. [Endpoints REST (Catálogo Completo)](#4-endpoints-rest-catálogo-completo)
5. [Modelos de Domínio (DTOs)](#5-modelos-de-domínio-dtos)
6. [Tributação e Mapeamento Fiscal](#6-tributação-e-mapeamento-fiscal)
7. [Persistência (PostgreSQL + EF Core)](#7-persistência-postgresql--ef-core)
8. [Numeração Sequencial Atômica](#8-numeração-sequencial-atômica)
9. [Histórico de Emissões / Auditoria](#9-histórico-de-emissões--auditoria)
10. [Certificado Digital A1 (.pfx)](#10-certificado-digital-a1-pfx)
11. [DANFE / PDF e HTML](#11-danfe--pdf-e-html)
12. [Segurança](#12-segurança)
13. [Validação de Entrada](#13-validação-de-entrada)
14. [Observabilidade](#14-observabilidade)
15. [Health Check](#15-health-check)
16. [Configuração e Variáveis de Ambiente](#16-configuração-e-variáveis-de-ambiente)
17. [Deploy / Docker / Easypanel](#17-deploy--docker--easypanel)
18. [Qualidade, CI e Testes](#18-qualidade-ci-e-testes)
19. [Contratos de Resposta e Códigos HTTP](#19-contratos-de-resposta-e-códigos-http)
20. [Limitações Conhecidas e Não-Capacidades](#20-limitações-conhecidas-e-não-capacidades)
21. [Roadmap Resumido](#21-roadmap-resumido)

---

## 1. Visão Geral

| Item | Valor |
|---|---|
| Stack | ASP.NET Core 8 (Controllers) — `.NET 8.0` |
| Linguagem | C# 12, `Nullable` habilitado, `ImplicitUsings` ligado |
| Bibliotecas fiscais | `Zeus.Net.NFe.NFCe`, `Zeus.Net.CTe`, `Zeus.Net.MDFe` (família DFe.NET) |
| ORM / Banco | EF Core 8 + Npgsql + **PostgreSQL 16** |
| Infra / Deploy | Docker (Linux), Docker Compose, Easypanel-friendly |
| Observabilidade | Serilog (console + arquivo rotativo) + OpenTelemetry OTLP opcional |
| Segurança | API Key (`X-Api-Key`) + Rate Limiting global por IP |
| Versão | `1.0.0` — `FiscalService.Api.csproj` |

Objetivo: **backend fiscal centralizado** consumível por qualquer cliente HTTP
(ex.: ERPs Node/NestJS, integrações internas), sem dependência de Windows ou GDI+.

---

## 2. Arquitetura e Camadas

```
┌────────────────────────────────────────────────────────────────┐
│ HTTP                                                           │
│  └─► RateLimiter (janela fixa, por IP, exceto /health)         │
│       └─► ApiKeyMiddleware (X-Api-Key, ApiKeyRing)             │
│            └─► Controllers (Thin)                              │
│                 └─► Services (Transient)                       │
│                      ├─► DFe.NET (SOAP/HTTPS → SEFAZ)          │
│                      └─► AppDbContext (Npgsql)                 │
└────────────────────────────────────────────────────────────────┘
```

- Controllers — `src/FiscalService.Api/Controllers/` (9 controllers).
- Services — `src/FiscalService.Api/Services/` (orquestração + DFe.NET + persistência de logs).
- Models — DTOs de entrada/saída em `Models/Requests` e `Models/Responses`.
- Data — `Data/AppDbContext.cs` + `Data/Entities` (EmissaoLog, NumeracaoSequencial).
- Helpers — `Helpers/UfHelper.cs` (UF → `Estado` IBGE).
- Infra — `Configuration/`, `Config/`, `Middlewares/`, `Telemetry/`, `Swagger/`.

**Decisão crítica:** todos os serviços fiscais são **`Transient`** — `DFe.NET` **não é
thread-safe**; cada requisição instancia um pipeline isolado.

---

## 3. Documentos Fiscais Suportados

| Documento | Modelo | Versão | Operações disponíveis |
|---|---|---|---|
| **NF-e** | `55` | 4.0 | Emitir · Cancelar · CC-e (Carta de Correção) · Consultar SEFAZ · Inutilizar faixa · Status SEFAZ |
| **NFC-e** | `65` | 4.0 | Emitir (com CSC, IdCSC, QR Code v1/v2/v3) · Cancelar |
| **CT-e** | `57` | 4.0 | Emitir · Cancelar |
| **MDF-e** | `58` | 3.0 | Emitir · Encerrar · Cancelar |

Todos os ambientes: **Homologação** ou **Produção** (campo `ambiente` no emitente).

Cada documento autorizado é registrado em `EmissaoLog` (CNPJ, modelo, série,
chave, protocolo, cStat, ambiente, datas).

---

## 4. Endpoints REST (Catálogo Completo)

Todos exigem header `X-Api-Key` exceto `/health`. Formato JSON em request/response.

### 4.1 NF-e — `NFeController` (`/api/nfe`)

| Método | Rota | Operação |
|---|---|---|
| POST | `/api/nfe/emitir` | Emite NF-e 4.0 (lote 1, síncrono) |
| POST | `/api/nfe/cancelar` | Evento de cancelamento (cStat 135) |
| POST | `/api/nfe/carta-correcao` | CC-e (sequência 1–20, mín. 15 caracteres) |
| POST | `/api/nfe/consultar` | `NfeConsultaProtocolo` por chave de 44 dígitos |
| POST | `/api/nfe/inutilizar` | `NfeInutilizacao` de faixa (mesma série) |
| GET | `/api/nfe/status-sefaz` | `NfeStatusServico` (querystring com dados de emitente) |

### 4.2 NFC-e — `NFCeController` (`/api/nfce`)

| Método | Rota | Operação |
|---|---|---|
| POST | `/api/nfce/emitir` | Emite NFC-e 4.0 com CSC/IdCSC e preenche `infNFeSupl` (qrCode + urlChave) |
| POST | `/api/nfce/cancelar` | Cancelamento (mesmo evento da NF-e) |

### 4.3 CT-e — `CTeController` (`/api/cte`)

| Método | Rota | Operação |
|---|---|---|
| POST | `/api/cte/emitir` | Emite CT-e 4.0 (modais 1–6) |
| POST | `/api/cte/cancelar` | Evento de cancelamento via `EventoCancelamento` |

### 4.4 MDF-e — `MDFeController` (`/api/mdfe`)

| Método | Rota | Operação |
|---|---|---|
| POST | `/api/mdfe/emitir` | Emite MDF-e 3.0 (rodoviário, com docs CT-e/NF-e vinculados) |
| POST | `/api/mdfe/encerrar` | Encerramento com UF + município de encerramento |
| POST | `/api/mdfe/cancelar` | Evento de cancelamento |

### 4.5 DANFE / PDF — `DanfeController` (`/api/danfe`)

| Método | Rota | Operação |
|---|---|---|
| POST | `/api/danfe/nfe` | Gera DANFE NF-e a partir do XML `nfeProc` (PDF base64) |
| POST | `/api/danfe/nfe/html` | Gera **HTML** imprimível a partir do `nfeProc` (JSON com `html` ou `?inline=true` → `text/html`) |
| POST | `/api/danfe/nfce` | Gera DANFE NFC-e (cupom) — exige `idCsc` + `csc` (PDF base64) |
| POST | `/api/danfe/nfce/html` | Gera **HTML** da NFC-e (mesmo corpo que PDF; `idCsc`/`csc` ignorados na montagem) |

> **PDF:** em Linux a implementação nativa **não está disponível** (`sucesso=false`,
> `erro.tipo="NaoSuportado"`). **HTML:** disponível em qualquer SO — impressão / PDF
> ficam a cargo do navegador (`Ctrl+P`). Ver `docs/DANFE-ESTRATEGIA.md`.

### 4.6 Certificado — `CertificadoController` (`/api/certificado`)

| Método | Rota | Operação |
|---|---|---|
| POST | `/api/certificado/validar` | Decodifica `.pfx` em base64; retorna CN, CNPJ, validade, emissor, thumbprint |
| POST | `/api/certificado/upload` | JSON com `.pfx` em Base64; salva em `Fiscal:DiretorioCertificados` (sanitiza nome, valida senha) |
| POST | `/api/certificado/upload-arquivo` | `multipart/form-data`: campo arquivo (`.pfx`/`.p12`), `senha`; opcional `nome` para o arquivo salvo |

### 4.7 Numeração — `NumeracaoController` (`/api/numeracao`)

| Método | Rota | Operação |
|---|---|---|
| GET | `/api/numeracao/{cnpj}/{modelo}/{serie}` | Reserva próximo número (`SELECT FOR UPDATE`) |
| POST | `/api/numeracao/confirmar` | Ajusta contador para um valor explícito (após inutilização/correção) |

### 4.8 Consulta Geral — `ConsultaController` (`/api/consulta`)

| Método | Rota | Operação |
|---|---|---|
| POST | `/api/consulta/status-servico` | Status SEFAZ roteado por modelo (`NFe` / `NFCe` / `CTe` / `MDFe`) |

O body informa `configuracaoEmitente` + `modelo` (`NFe` default · aceita também
códigos `55`, `65`, `57`, `58`). Internamente delega para o service correspondente:

- `NFe` / `55` → `NFeService.ConsultarStatusSefaz` (`ServicosNFe.NfeStatusServico`).
- `NFCe` / `65` → `NFCeService.ConsultarStatusSefaz` (`ServicosNFe` com `ModeloDocumento.NFCe`).
- `CTe` / `57` → `CTeService.ConsultarStatusSefaz` (`CTe.Servicos.ConsultaStatus.StatusServico.ConsultaStatusV4`).
- `MDFe` / `58` → `MDFeService.ConsultarStatusSefaz` (`ServicoMDFeStatusServico.MDFeStatusServico`).

Modelo desconhecido retorna `sucesso=false` + `erro.tipo="ModeloInvalido"`.

### 4.9 Histórico de Emissões — `EmissoesController` (`/api/emissoes`)

| Método | Rota | Operação |
|---|---|---|
| GET | `/api/emissoes` | Lista paginada do log de emissões com filtros (CNPJ, modelo, série, ambiente, status, chave, datas) |
| GET | `/api/emissoes/{chave}` | Último registro pela chave de acesso (44 dígitos) |

Query string suportada na listagem:

| Parâmetro | Tipo | Descrição |
|---|---|---|
| `cnpj` | string | Filtra pelo emitente (14 dígitos sem máscara) |
| `modelo` | string | `55`, `65`, `57` ou `58` |
| `serie` | string | Série do documento |
| `ambiente` | string | `Homologacao` ou `Producao` |
| `status` | string | `Autorizado`, `Cancelado`, `Rejeitado`, `Inutilizado` |
| `chave` | string | Chave de acesso de 44 dígitos |
| `dataDe` / `dataAte` | DateTime UTC | Janela inclusiva sobre `dataEmissao` |
| `pagina` | int | 1-based (default `1`) |
| `tamanhoPagina` | int | Default `50`, mínimo `1`, máximo `200` |

Resposta:

```jsonc
{
  "itens": [ /* EmissaoLogResponse[] */ ],
  "pagina": 1,
  "tamanhoPagina": 50,
  "total": 1234,
  "totalPaginas": 25,
  "temProxima": true
}
```

Ordenação: `dataEmissao DESC`. Sem registros → `total = 0` e `itens` vazia.

### 4.10 Saúde — Endpoint global

| Método | Rota | Observações |
|---|---|---|
| GET | `/health` | Sem autenticação · sem rate limit · payload custom (status + banco + schemas) |

### 4.11 Documentação interativa

- **Swagger UI** em `/swagger` no ambiente `Development` — inclui:
  - Definição de segurança `ApiKey` no header `X-Api-Key`.
  - Filtro `OpenApiCommonResponsesOperationFilter` que documenta respostas
    `400`, `401`, `422` e `429` em todas as operações.

---

## 5. Modelos de Domínio (DTOs)

### 5.1 Request comum: `ConfiguracaoEmitenteRequest`

Reutilizado em **todos** os endpoints fiscais (corpo da requisição).

| Campo | Obrigatório | Descrição |
|---|---|---|
| `cnpj` | Sim | 14 dígitos (validado via FluentValidation) |
| `razaoSocial` | Sim | Até 120 caracteres |
| `nomeFantasia` | Não | |
| `ie` | Não | Inscrição estadual |
| `crt` | Sim | 1 = Simples · 2 = Simples Excesso · 3 = Regime Normal |
| `uf` | Sim | 2 letras (mapeado para `Estado` IBGE em `UfHelper`) |
| `endereco` | Não | `EnderecoRequest` (logradouro, nº, bairro, município, CEP, código IBGE etc.) |
| `ambiente` | Sim | `"Homologacao"` ou `"Producao"` |
| `certificadoPath` | Sim | Path absoluto ou relativo a `Fiscal:DiretorioCertificados` |
| `certificadoSenha` | Sim | Senha do `.pfx` |

### 5.2 NF-e — `NFeEmitirRequest`

| Campo | Default | Notas |
|---|---|---|
| `numeroNota` | 0 | obrigatório, `> 0` |
| `serie` | "1" | 1–999 |
| `naturezaOperacao` | "Venda de Mercadoria" | |
| `finalidade` | 1 | 1=Normal · 2=Complementar · 3=Ajuste · 4=Devolução |
| `tipoOperacao` | 1 | 0=Entrada · 1=Saída |
| `indicadorDestinatario` | 1 | 1=Interna · 2=Interestadual · 3=Exterior |
| `modalidadeFrete` | 1 | 0–9 (códigos SEFAZ) |
| `destinatario` | — | `DestinatarioRequest` (CNPJ ou CPF, IE, indicador IE) |
| `itens` | [] | Lista de `ItemNFeRequest` (não vazia) |
| `pagamentos` | [] | Lista de `PagamentoRequest` |
| `informacoesAdicionais` | null | Texto livre (`infCpl`) |

### 5.3 NFC-e — `NFCeEmitirRequest`

Adiciona ao padrão NF-e:

| Campo | Obrigatório | Notas |
|---|---|---|
| `idCsc` | Sim | Identificador do CSC (numérico, normalizado pela DFe.NET) |
| `csc` | Sim | Código de Segurança do Contribuinte |
| `qrCodeVersao` | Não | `"1"`, `"2"` (default) ou `"3"` |

### 5.4 CT-e — `CTeEmitirRequest`

| Campo | Notas |
|---|---|
| `cfop` | 4 dígitos (padrão `"6351"`, validado) |
| `modal` | `"01"` Rodoviário · `"02"` Aéreo · `"03"` Aquaviário · `"04"` Ferroviário · `"05"` Dutoviário · `"06"` Multimodal |
| `remetente`, `destinatario`, `tomador` | Pessoa jurídica/física com endereço |
| `valorTotalServico`, `valorTotalCarga` | decimais |
| `documentos` | Lista de NF-e/CT-e vinculados |

### 5.5 MDF-e — `MDFeEmitirRequest`

| Campo | Notas |
|---|---|
| `modal` | `01`–`04` |
| `ufInicio` / `ufFim` | 2 letras |
| `dataHoraInicio` | `DateTime` UTC |
| `municipiosCarregamento` | Código IBGE + nome |
| `percurso` | Lista de UFs |
| `documentos` | NF-e ou CT-e vinculados (`tipoDocumento = "CTe" | "NFe"`) |

### 5.6 Item Fiscal — `ItemNFeRequest`

Cobre **NF-e e NFC-e**. Campos essenciais:

- Identificação: `codigoProduto`, `codigoEan` (ou `"SEM GTIN"`), `descricaoProduto`, `ncm`, `cest`, `cfop`.
- Comerciais: `unidadeComercial`, `quantidadeComercial`, `valorUnitarioComercial`, `valorTotalBruto`, `indicadorTotal`.
- Tributáveis: variantes `unidadeTributavel`, `quantidadeTributavel`, `valorUnitarioTributavel`.
- ICMS: `cstIcms` ou `csosnIcms`, `origemMercadoria`, base, alíquota, valor; suporte a ICMS-ST e FCP-ST (CST 60, CSOSN 201/202/203/500/900).
- PIS / COFINS: CST + base + alíquota + valor.
- IPI: CST + valor.
- Outros: `valorDesconto`, `valorFrete`, `valorSeguro`, `valorOutrasDespesas`, `informacaoAdicional`.

### 5.7 Pagamento — `PagamentoRequest`

`formaPagamento` (`01`=Dinheiro, `03`=Crédito, `04`=Débito, `17`=PIX, `90`=Sem
pagamento, `99`=Outros, etc.) + `valorPagamento` + opcionais para cartão
(`bandeiraCartao`, `cnpjCredenciadora`, `numeroAutorizacao`, `tipoIntegracao`).

### 5.8 Resposta padrão — `FiscalResponse`

```jsonc
{
  "sucesso": true,
  "chaveAcesso": "35250100000000000000550010000000011000000019",
  "protocolo": "135250000000001",
  "codigoStatus": "100",
  "mensagem": "Autorizado o uso da NF-e",
  "xmlAutorizado": "<nfeProc>…</nfeProc>",
  "xmlEnviado": null,
  "xmlRetorno": null,
  "danfePdfBase64": "JVBERi0x…",
  "erro": null
}
```

Em falha:

```jsonc
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

Tipos de erro classificados (`ClassificarExcecao`):
`RejeicaoSefaz`, `CertificadoInvalido`, `ServicoIndisponivel`,
`ValidacaoSchema`, `NaoAutorizado`, `LimiteExcedido`, `NaoSuportado`,
`ErroInterno`.

---

## 6. Tributação e Mapeamento Fiscal

`Services/Fiscal/ImpostoItemFactory.cs` + `ImpostoIcmsMapper.cs` montam o grupo
`<imposto>` do XML conforme:

- **CRT 3 (Regime Normal):**
  - CST `00` → `ICMS00` (operação tributada)
  - CST `40 / 41 / 50` → `ICMS40` (isenta / não-tributada / suspensão, com desoneração opcional)
  - CST `60` → `ICMS60` (ST retido — base, alíquota, FCP-ST, valores efetivos)
- **CRT 1/2 (Simples Nacional):**
  - CSOSN `101`, `102` (default), `103`, `201`, `202`, `203`, `500`, `900`
  - Cobre crédito permitido, ST, FCP-ST, redução de base e cenários "outros".
- **PIS / COFINS:** sempre `PISAliq` / `COFINSAliq` (CST default `07` quando ausente).
- **Origem da mercadoria:** numérico 0–8 (`OrigemMercadoria`).

A normalização de CST (2 dígitos) e CSOSN (3 dígitos) é feita na própria fábrica
para evitar erros de schema SEFAZ.

---

## 7. Persistência (PostgreSQL + EF Core)

`AppDbContext` com **migrations automáticas** no startup
(`db.Database.MigrateAsync()`). Schema inicial: `20260424145436_InitialCreate`.

### 7.1 `emissao_logs`

Auditoria de toda emissão / evento bem-sucedido pelos serviços fiscais.

| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | `bigserial` PK | |
| `cnpj` | varchar(14) | Emitente |
| `modelo` | varchar(2) | `"55"`, `"65"`, `"57"`, `"58"` |
| `serie` | varchar(3) | |
| `numero` | int | |
| `chave_acesso` | varchar(44) | índice `ix_emissao_logs_chave_acesso` |
| `protocolo` | varchar(20) | |
| `status` | varchar(20) | `Autorizado` / `Cancelado` / `Rejeitado` / `Inutilizado` — índice `ix_emissao_logs_status` |
| `codigo_status` | varchar(3) | cStat retornado |
| `mensagem_status` | varchar(500) | xMotivo |
| `ambiente` | varchar(20) | |
| `data_emissao` / `data_processamento` | timestamp | índice composto `ix_emissao_logs_cnpj_data` |
| `xml_path` | varchar(500) | (reservado) |

Atualização de status (cancelamento) via `AtualizarStatusLogAsync` por chave.

### 7.2 `numeracoes_sequenciais`

| Coluna | Tipo | Descrição |
|---|---|---|
| `id` | `bigserial` PK | |
| `cnpj` | varchar(14) | |
| `modelo` | varchar(2) | |
| `serie` | varchar(3) | |
| `ultimo_numero` | int | |
| `ultima_atualizacao` | timestamp | |

Índice **único** composto `ix_numeracoes_cnpj_modelo_serie` (CNPJ + modelo +
série).

---

## 8. Numeração Sequencial Atômica

`NumeracaoService` garante que duas requisições concorrentes **nunca** produzam
o mesmo número:

```sql
SELECT * FROM numeracoes_sequenciais
WHERE cnpj = $1 AND modelo = $2 AND serie = $3
FOR UPDATE
```

1. Abre transação `BeginTransactionAsync`.
2. `FromSqlRaw(... FOR UPDATE)` (**lock pessimista** PostgreSQL).
3. Cria com `UltimoNumero = 1` se não existir, senão incrementa.
4. `SaveChanges` + `Commit`. Em erro → `Rollback`.

Métodos:
- `ObterProximoNumeroAsync(cnpj, modelo, serie)` — reserva atômica.
- `ConsultarUltimoNumeroAsync(cnpj, modelo, serie)` — leitura sem lock.
- `ConfirmarNumeroAsync(cnpj, modelo, serie, numero)` — força contador para um
  valor maior (útil após inutilização ou correção manual).

### 8.1 Sincronização emissão → numeração

Após **toda** emissão autorizada (NF-e, NFC-e, CT-e e MDF-e), o respectivo
service chama `NumeracaoService.ConfirmarNumeroAsync(cnpj, modelo, serie, numero)`
em um bloco `try/catch` (warning de log se falhar — não derruba a emissão).
Isso mantém o contador do banco em paridade com o número efetivamente aceito
pela SEFAZ, mesmo quando o cliente ignorar `/api/numeracao/proximo` e numerar
manualmente.

---

## 9. Histórico de Emissões / Auditoria

`EmissaoLogService` consulta o log persistido em `emissao_logs` e expõe uma
camada paginada via `EmissoesController`:

- `ListarAsync(...)` aplica filtros opcionais (CNPJ, modelo, série, ambiente,
  status, chave, janela de datas) e devolve `PagedResponse<EmissaoLogResponse>`
  ordenado por `dataEmissao DESC` com `total` e `totalPaginas`.
- `ObterPorChaveAsync(chave)` retorna o registro **mais recente** para uma
  chave de acesso (tipicamente o último estado conhecido de uma nota).

Tamanho de página: default `50`, mínimo `1`, máximo `200` (clamp em
`EmissaoLogService.TamanhoPaginaMaximo`).

Casos de uso típicos:

- Painéis administrativos com status atual de cada nota.
- Conciliação contábil (`status = "Cancelado"`, ambiente `Producao`).
- Reprocessamento / suporte (busca por chave de 44 dígitos).

---

## 10. Certificado Digital A1 (.pfx)

`CertificadoService` (`Services/CertificadoService.cs`):

- **Validar:** decodifica base64, instancia `X509Certificate2` com
  `EphemeralKeySet`, extrai do `Subject` no padrão ICP-Brasil
  `CN=RAZAO SOCIAL:CNPJ` → retorna `cnpj`, `razaoSocial`, `validade`,
  `emissor`, `thumbprint`.
- **Upload (JSON):** `ConteudoBase64` + `Nome` + `Senha` — valida (mesmo fluxo) e grava bytes em
  `Fiscal:DiretorioCertificados`. Sanitiza o nome (remove diretórios,
  garante extensão `.pfx`/`.p12`).
- **Upload (multipart):** `POST /api/certificado/upload-arquivo` — campo `arquivo` (`.pfx`/`.p12`), `senha`; opcional `nome` para o arquivo salvo; mesmo fluxo de validação e gravação.
- **CarregarCertificado(path, senha):** uso interno; resolve path relativo,
  abre `X509Certificate2` com `EphemeralKeySet | Exportable`.

Resolução de path:
`FiscalConfig.ResolveCertificadoPath(p) = Path.IsPathRooted(p) ? p : Path.Combine(DiretorioCertificados, p)`.

> **Avisos de segurança:** `.pfx` **nunca** vai ao Git. Em produção use
> volume persistente em `/app/certificados` ou injete via secret manager.

---

## 11. DANFE / PDF e HTML

### 11.1 PDF (base64)

Rotas `POST /api/danfe/nfe` e `POST /api/danfe/nfce` retornam:

```jsonc
{ "sucesso": true|false, "pdfBase64": "…", "erro": { "tipo": "NaoSuportado", … } }
```

Estado atual do **PDF**: **`DanfeService` lança `NotSupportedException` em Linux**. Motivo: o
pacote `NFe.Danfe.Nativo` é Windows-centric (GDI+/`System.Drawing`).

### 11.2 HTML (impressão pelo navegador)

Rotas **`POST /api/danfe/nfe/html`** e **`POST /api/danfe/nfce/html`**:

- Corpo: mesmo JSON das rotas PDF (`xmlNfeProc`; NFC-e inclui `idCsc` e `csc` por compatibilidade).
- **Padrão:** resposta JSON `{ "sucesso": true, "html": "<!DOCTYPE html>…" }` (`DanfeHtmlResponse`).
- **`?inline=true`:** resposta `Content-Type: text/html; charset=utf-8` com o documento completo (abrir em nova aba / imprimir).

O HTML gerado cobre **identificação (ide)**, **NFref**, **emitente/destinatário** (com endereço), **autXML**,
**retirada/entrega**, **protocolo**, **itens** (produto, NCM, CFOP, quantidades, **resumo de impostos** por linha),
**ICMSTot** (e ISSQNtot / retTrib quando existirem), **cobrança/fatura/duplicatas**, **transporte** (retTransp,
transporta, veículo, volumes), **pagamentos** (cartão quando informado), **compra/exporta**, **infAdic**
(infCpl, obsCont, obsFisco). Ainda **não** reproduz 100% do desenho gráfico oficial do MOC (ex.: grade de
campos idêntica ao PDF SEFAZ).

### 11.3 Evolução (PDF profissional)

Caminhos adicionais em `docs/DANFE-ESTRATEGIA.md`:

1. Microsserviço externo dedicado (worker Windows, QuestPDF, etc.).
2. Biblioteca .NET cross-platform (DanfeSharp / QuestPDF).
3. Geração local no cliente a partir do `xmlAutorizado`.

Durante a emissão NF-e/NFC-e o DANFE **PDF** é tentado **best-effort**: se falhar, o
`FiscalResponse` ainda é `sucesso=true` com `danfePdfBase64=null` (a operação
fiscal não é comprometida).

---

## 12. Segurança

### 12.1 Autenticação por API Key

`ApiKeyMiddleware` valida o header `X-Api-Key` em **todas** as rotas exceto
`/health`. Em falha: `401 Unauthorized` + body JSON
`{ sucesso:false, erro:{ tipo:"NaoAutorizado", … } }`.

### 12.2 Anel de chaves (`ApiKeyRing`)

`ApiKey` aceita **uma ou várias** chaves separadas por `,`, `|`, `;` ou
quebras de linha. Comparação **ordinal** (case-sensitive). Permite **rotação
sem downtime**:

- `API_KEY` = chave nova
- `API_KEY_PREVIOUS` = chave antiga (durante a janela de transição)
- O `EnvBootstrap` mescla automaticamente: `ApiKey = "nova,antiga"`.

### 12.3 Rate Limiting global

`AddRateLimiter` com `PartitionedRateLimiter` por **IP de origem**, janela fixa:

| Configuração | Default | Variável |
|---|---|---|
| Liga/Desliga | `true` | `RateLimiting__Enabled` |
| Permits por janela | `180` | `RateLimiting__PermitLimit` |
| Janela (segundos) | `60` | `RateLimiting__WindowSeconds` |

`/health` é **sempre** liberado (sem limite). Quando excedido: `429` com
`erro.tipo="LimiteExcedido"`.

### 12.4 Boas práticas reforçadas pelo código

- `DataAnnotations` + **FluentValidation** previnem ataques por payloads
  malformados.
- `EphemeralKeySet` no `X509Certificate2` evita vazamento de chave privada na
  store do SO.
- Sanitização de nome de arquivo no upload de certificado.
- Senha de banco e API Key **nunca** no `appsettings.json`; são lidas do
  ambiente / `.env` via `EnvBootstrap`.

---

## 13. Validação de Entrada

Camada dupla:

1. **DataAnnotations** nos DTOs (`[Required]`, `[StringLength]`, `[MinLength]`, `[Range]`).
2. **FluentValidation** em `Validation/`:
   - `ConfiguracaoEmitenteRequestValidator` — CNPJ 14 dígitos, UF 2 letras,
     `Ambiente` ∈ {Homologacao, Producao}, CRT 1–3, certificado obrigatório.
   - `ItemNFeRequestValidator` — código/descrição/unidade obrigatórios, valores
     ≥ 0, CFOP de 4 dígitos, CST até 2 dígitos, CSOSN exatamente 3 dígitos.
   - `NFeEmitirRequestValidator` — número da nota > 0, série 1–999, finalidade
     1–4, tipo operação 0–1, indicador destinatário 1–3, modalidade frete 0–9,
     itens não vazios.
   - `NFCeEmitirRequestValidator` — exige CSC + IdCSC, QR Code v1/v2/v3, itens
     e pagamentos não vazios.
   - `CTeEmitirRequestValidator` — CFOP, modal 1–6, partes obrigatórias.
   - `MDFeEmitirRequestValidator` — modal `01`–`04`, UFs 2 letras, municípios
     de carregamento e documentos não vazios.

Falhas viram `400 Bad Request` com `ModelState`/`ProblemDetails` detalhado.

---

## 14. Observabilidade

### 14.1 Logs (Serilog)

- `Serilog.AspNetCore` enriquecido com `FromLogContext`, `WithMachineName`,
  `WithThreadId`.
- **Console** sempre ativo.
- **Arquivo rotativo** opcional (sink de arquivo) — habilitado apenas se
  `Serilog:File` existir, não estiver `Disabled` e o diretório for
  **gravável**. `SerilogFileSinkHelper` faz um *probe* (`.tmp`) e **degrada
  graciosamente** para console-only quando o FS é somente leitura.
- `app.UseSerilogRequestLogging` adiciona ao log de request: `RemoteIP` e
  `RequestHost`.

### 14.2 Métricas / Tracing (OpenTelemetry, opcional)

Ativado quando há endpoint OTLP (env `OTEL_EXPORTER_OTLP_ENDPOINT` ou
`OpenTelemetry__OtlpEndpoint`). Quando ligado:

- **Resource:** `service.name = FiscalService`.
- **Tracing:** ASP.NET Core + HttpClient + OTLP exporter.
- **Metrics:** ASP.NET Core + HttpClient + Meter custom `FiscalService` + OTLP exporter.

#### Métrica fiscal customizada

`FiscalTelemetry.RecordSefazOutcome(operation, sucesso, cStatOuTipo)`:

| Métrica | Tipo | Tags |
|---|---|---|
| `fiscal.sefaz.outcomes` | Counter `long` | `operation`, `sucesso`, `cstat` |

O filtro `FiscalResponseTelemetryFilter` (registrado globalmente) intercepta
qualquer `ObjectResult` cujo `Value` é `FiscalResponse` e emite o ponto de
métrica automaticamente — **toda emissão / evento gera telemetria**.

---

## 15. Health Check

`GET /health` (sem auth, sem rate limit):

```json
{
  "status": "healthy",
  "versao": "1.0.0",
  "timestamp": "2025-04-24T10:00:00Z",
  "banco": "healthy",
  "schemas": "ok"
}
```

Verificações:
- **`postgresql`** via `AspNetCore.HealthChecks.NpgSql` (tags `db`, `sql`).
- **Schemas XSD** — checa existência do diretório `Fiscal:DiretorioSchemas`
  (necessário para validação XML do DFe.NET).

---

## 16. Configuração e Variáveis de Ambiente

### 16.1 Bootstrap (`EnvBootstrap`)

Antes do `WebApplication.CreateBuilder`:

1. **Localiza `.env`** subindo até 14 níveis acima do `cwd` (ou do `BaseDirectory`).
2. Carrega via **DotNetEnv**.
3. Aplica aliases (estilo Docker → estilo ASP.NET):
   - `API_KEY` → `ApiKey`
   - `API_KEY_PREVIOUS` → mesclada em `ApiKey`
   - `FISCAL_AMBIENTE` → `Fiscal__Ambiente`
   - `FISCAL_TIMEOUT_WS` → `Fiscal__TimeoutWs`
4. Constrói `Database__ConnectionString` quando ausente:
   - Prioridade 1: `DATABASE_URL` (URI `postgres://`/`postgresql://` → Npgsql).
   - Prioridade 2: `DB_PASSWORD` se for URI Postgres (legado).
   - Prioridade 3: `Host=DB_HOST;Port=DB_PORT;Database=DB_NAME;Username=DB_USER;Password=DB_PASSWORD`.
5. `sslmode` da URI é traduzido para `SslMode` Npgsql
   (`disable | allow | prefer | require | verify-ca | verify-full`).

### 16.2 Seções de configuração

| Seção | Classe | Função |
|---|---|---|
| `ApiKey` | string | Chave(s) válida(s) (ver `ApiKeyRing`) |
| `RateLimiting` | `RateLimitingConfig` | `Enabled`, `PermitLimit`, `WindowSeconds` |
| `OpenTelemetry` | `OpenTelemetryConfig` | `Enabled`, `OtlpEndpoint` |
| `Fiscal` | `FiscalConfig` | `Ambiente`, `SalvarXmls`, `DiretorioXmls`, `DiretorioSchemas`, `DiretorioCertificados`, `TimeoutWs` |
| `Database` | string | `ConnectionString` Npgsql |
| `Serilog` | nativo | `MinimumLevel`, `WriteTo`, `File`, `Enrich` |

### 16.3 Variáveis principais (resumo prático)

| Variável | Descrição | Default |
|---|---|---|
| `API_KEY` | Header `X-Api-Key` | — |
| `API_KEY_PREVIOUS` | Chave de rotação | — |
| `DATABASE_URL` | URI Postgres completa | — |
| `DB_PASSWORD` / `DB_HOST` / `DB_PORT` / `DB_NAME` / `DB_USER` | Montagem alternativa | `localhost`, `5432`, `fiscal_db`, `fiscal_user` |
| `Database__ConnectionString` | Connection string explícita | — |
| `FISCAL_AMBIENTE` | `Homologacao` ou `Producao` | `Homologacao` |
| `FISCAL_TIMEOUT_WS` | Timeout SEFAZ (s) | `30` |
| `RateLimiting__Enabled` | Liga rate limit | `true` |
| `RateLimiting__PermitLimit` | Permits/janela | `180` |
| `RateLimiting__WindowSeconds` | Janela (s) | `60` |
| `OpenTelemetry__Enabled` / `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP | desligado |
| `Serilog__File__Disabled` | Força console-only | `false` |
| `Serilog__File__Path` | Override do caminho do log | `/app/logs/fiscal-.log` |
| `SERVICE_PORT` | Porta exposta no Compose | `5555` |

---

## 17. Deploy / Docker / Easypanel

### 17.1 Imagem (`src/FiscalService.Api/Dockerfile`)

- Multi-stage:
  - **Build:** `mcr.microsoft.com/dotnet/sdk:8.0` (`restore` → `build`).
  - **Publish:** `dotnet publish -c Release -o /app/publish /p:UseAppHost=false`.
  - **Runtime:** `mcr.microsoft.com/dotnet/aspnet:8.0`.
- Pacotes de runtime instalados: `libfontconfig1`, `libfreetype6`, `libx11-6`,
  `libxext6`, `libxrender1`, `fonts-dejavu-core`, `fonts-liberation`, `curl`
  (este último para o `HEALTHCHECK`).
- Cria `/app/{xmls,schemas,certificados,logs}` (sobrescritos por volumes em
  produção). Schemas XSD do DFe.NET são copiados em `/app/schemas`.
- Variáveis padrão: `ASPNETCORE_URLS=http://+:8080`,
  `ASPNETCORE_ENVIRONMENT=Production`.
- `HEALTHCHECK`: `curl -f http://localhost:8080/health`.

### 17.2 Compose (`docker-compose.yml`)

Sobe dois serviços:

| Serviço | Imagem | Função |
|---|---|---|
| `fiscal-service` | build local | API (`${SERVICE_PORT:-5555}:8080`) |
| `db` | `postgres:16-alpine` | Banco com `pg_isready` healthcheck |

Volumes nomeados (persistência): `fiscal_xmls`, `fiscal_certs`, `fiscal_logs`,
`fiscal_pgdata`.

### 17.3 Easypanel / Painéis Docker

- **Build context:** raiz do repositório (não a pasta do projeto).
- **Dockerfile:** `src/FiscalService.Api/Dockerfile`.
- **Porta:** `8080` interna.
- **Variáveis mínimas:** `ApiKey`, `Database__ConnectionString` (ou `DB_PASSWORD` + helpers).
- **Persistência:** monte volumes em `/app/xmls`, `/app/certificados`, `/app/logs`.
- **Pós-deploy:** seguir `docs/SMOKE-HOMOLOGACAO.md` (saúde, auth, NFC-e/QR,
  numeração, certificado).

---

## 18. Qualidade, CI e Testes

### 18.1 GitHub Actions (`.github/workflows/ci.yml`)

Job `build-test-docker` em `ubuntu-latest`:

1. `actions/setup-dotnet@v4` (`8.0.x`).
2. `dotnet restore FiscalService.sln`
3. `dotnet build FiscalService.sln --no-restore -c Release`
4. `dotnet test FiscalService.sln --no-build -c Release`
5. `docker build -f src/FiscalService.Api/Dockerfile .`

### 18.2 Testes Unitários — `tests/FiscalService.Api.Tests`

Cobertura inclui validators, helpers e mappers (xUnit + `FluentValidation.TestHelper`):

| Suite | Foco |
|---|---|
| `ApiKeyRingTests` | Parsing de múltiplas chaves separadas por `,`, `|`, `;` ou linha. |
| `ConfiguracaoEmitenteRequestValidatorTests` | CNPJ, UF, Ambiente, certificado. |
| `NFeEmitirRequestValidatorTests` | Número, série, finalidade, indicador destino, frete, itens. |
| `NFCeEmitirRequestValidatorTests` | CSC + IdCSC obrigatórios, QR Code v1/v2/v3, pagamentos não-vazios. |
| `CTeEmitirRequestValidatorTests` | CFOP de 4 dígitos, modal `1`–`6`, partes obrigatórias. |
| `MDFeEmitirRequestValidatorTests` | Modal `01`–`04`, UFs, municípios e documentos não-vazios. |
| `ItemNFeRequestValidatorTests` | Quantidade > 0, CFOP regex, CST 1–2 dígitos, CSOSN exatos 3 dígitos. |
| `UfHelperTests` | Mapeamento `UF → Estado` para 27 unidades + invalidações. |
| `ImpostoIcmsMapperTests` | CRT 3 (`ICMS00/40/60`) e CRT 1/2 (`ICMSSN101/102/201/202/500/900`). |

### 18.3 Testes de Integração — `tests/FiscalService.Api.IntegrationTests`

- **Testcontainers** sobe um Postgres real, aplica `MigrateAsync` e exercita
  `NumeracaoService`:
  - Primeira reserva retorna `1`.
  - Reservas consecutivas incrementam (`1, 2, …`).
  - `ConfirmarNumeroAsync` ajusta o contador para cima.
- `EmissaoLogServiceIntegrationTests`:
  - Listagem paginada com filtros (CNPJ + modelo) — calcula `total`,
    `totalPaginas` e `temProxima`.
  - `ObterPorChaveAsync` retorna o registro **mais recente** quando há
    múltiplos eventos para a mesma chave (autorizado → cancelado).
- `DockerHostGuard` + `[SkippableFact]` — quando `docker info` não está
  disponível, os testes são **ignorados** localmente e **executados** no CI
  (Ubuntu + Docker).

---

## 19. Contratos de Resposta e Códigos HTTP

Documentados via `OpenApiCommonResponsesOperationFilter` (Swagger):

| HTTP | Quando ocorre | Body |
|---|---|---|
| `200 OK` | Operação bem-sucedida | `FiscalResponse` (ou específico) com `sucesso=true` |
| `400 Bad Request` | Falha de DataAnnotations / FluentValidation | `ProblemDetails` / `ModelState` |
| `401 Unauthorized` | `X-Api-Key` ausente ou inválida | `{ sucesso:false, erro:{ tipo:"NaoAutorizado" } }` |
| `422 Unprocessable Entity` | Falha de negócio (rejeição SEFAZ, certificado etc.) | `FiscalResponse` com `sucesso=false` |
| `429 Too Many Requests` | Rate limit por IP | `{ sucesso:false, erro:{ tipo:"LimiteExcedido" } }` |
| `500 Internal Server Error` | Reservado a falhas inesperadas (raras) | `{ sucesso:false, erro:{ tipo:"ErroInterno" } }` |

---

## 20. Limitações Conhecidas e Não-Capacidades

| Tema | Estado |
|---|---|
| **DANFE PDF em Linux** | Não suportado nativamente (PDF). **DANFE em HTML** disponível em `/api/danfe/nfe/html` e `/api/danfe/nfce/html`. Veja `docs/DANFE-ESTRATEGIA.md`. |
| **Certificado A3 / HSM** | Apenas A1 (`.pfx`) está implementado (`TipoCertificado.A1Arquivo`). |
| **Modais MDF-e ≥ 02** | Construção do modal apenas para **rodoviário** (`MDFeRodo`); demais não montados. |
| **CT-e tributação** | ICMS00 fixo a 12% no construtor — para cenários ICMS-ST/Reduzido use evolução do `CTeService`. |
| **Contingência SVC-AN/RS** | `tpEmis` fixo em `teNormal` na emissão NF-e/NFC-e (ver `RoadMap`). |
| **Distribuição DF-e / Manifestação** | Não exposto; pode ser adicionado consumindo `NFeDistribuicaoDFe`. |
| **Webhooks / Emissão assíncrona** | Não implementado — todos os endpoints são síncronos (`IndicadorSincronizacao.Sincrono`). |
| **Autenticação** | Apenas API Key compartilhada. Não há OAuth / JWT / mTLS. |
| **Multi-tenant nativo** | O multitenant é por CNPJ no payload; não há isolamento por organização (sem RLS). |

---

## 21. Roadmap Resumido

Da seção *Fase 3* do `PLANNING.md` (atualizado conforme o que já foi entregue):

- ✅ Status SEFAZ por modelo (NF-e/NFC-e/CT-e/MDF-e).
- ✅ Sincronização emissão → numeração (`ConfirmarNumeroAsync` automático).
- ✅ Endpoint de consulta de **logs de emissão** com filtros e paginação
  (`GET /api/emissoes`, `GET /api/emissoes/{chave}`).
- ⏳ DANFE multiplataforma (DanfeSharp / QuestPDF / serviço externo).
- ⏳ Suporte a **contingência** SVC-AN / SVC-RS.
- ⏳ Endpoints **assíncronos com callback webhook**.
- ⏳ **Retry automático** em falha de conectividade SEFAZ.
- ⏳ Suporte a **certificado A3 / HSM**.
- ⏳ Distribuição DF-e / Manifestação do Destinatário.
- ⏳ Pipeline CI/CD ampliado e cobertura de testes ≥ 80%.

---

## Apêndice A — Arquivos-chave por capacidade

| Capacidade | Caminho |
|---|---|
| Bootstrap / `.env` | `src/FiscalService.Api/Configuration/EnvBootstrap.cs` |
| Anel de chaves | `src/FiscalService.Api/Configuration/ApiKeyRing.cs` |
| Auth | `src/FiscalService.Api/Middlewares/ApiKeyMiddleware.cs` |
| Rate limit | `src/FiscalService.Api/Program.cs` + `Config/RateLimitingConfig.cs` |
| Configuração fiscal | `src/FiscalService.Api/Config/FiscalConfig.cs` |
| Telemetria | `src/FiscalService.Api/Telemetry/FiscalTelemetry.cs`, `FiscalResponseTelemetryFilter.cs` |
| Logs | `src/FiscalService.Api/Configuration/SerilogFileSinkHelper.cs` |
| Swagger | `src/FiscalService.Api/Swagger/OpenApiCommonResponsesOperationFilter.cs` |
| EF Core | `src/FiscalService.Api/Data/AppDbContext.cs`, `Data/Entities/*.cs`, `Migrations/*` |
| Numeração | `src/FiscalService.Api/Services/NumeracaoService.cs` |
| Certificado | `src/FiscalService.Api/Services/CertificadoService.cs` |
| Histórico de emissões | `src/FiscalService.Api/Services/EmissaoLogService.cs`, `Controllers/EmissoesController.cs`, `Models/Responses/EmissaoLogResponse.cs` |
| Status SEFAZ multimodelo | `src/FiscalService.Api/Controllers/ConsultaController.cs` (delega aos 4 services) |
| NF-e | `src/FiscalService.Api/Services/NFeService.cs`, `Controllers/NFeController.cs` |
| NFC-e | `src/FiscalService.Api/Services/NFCeService.cs`, `Controllers/NFCeController.cs` |
| CT-e | `src/FiscalService.Api/Services/CTeService.cs`, `Controllers/CTeController.cs` |
| MDF-e | `src/FiscalService.Api/Services/MDFeService.cs`, `Controllers/MDFeController.cs` |
| DANFE | `src/FiscalService.Api/Services/DanfeService.cs`, `Controllers/DanfeController.cs`, `Services/DanfeHtml/DanfeHtmlRenderer.cs` |
| Tributação ICMS / PIS / COFINS | `src/FiscalService.Api/Services/Fiscal/ImpostoIcmsMapper.cs`, `ImpostoItemFactory.cs` |
| UF → IBGE | `src/FiscalService.Api/Helpers/UfHelper.cs` |
| Validações | `src/FiscalService.Api/Validation/*Validator.cs` |
| Testes | `tests/FiscalService.Api.Tests/`, `tests/FiscalService.Api.IntegrationTests/` |
| CI | `.github/workflows/ci.yml` |
| Docker / Compose | `src/FiscalService.Api/Dockerfile`, `docker-compose.yml` |
