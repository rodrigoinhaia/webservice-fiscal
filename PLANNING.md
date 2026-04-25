# PLANNING — FiscalService

## Objetivo

Criar um WebService REST em ASP.NET Core 8 que atue como **microsserviço de emissão fiscal brasileira**, consumível por qualquer aplicação (especialmente projetos Node.js/NestJS via HTTP). O serviço é o backend fiscal centralizado para os produtos **Diin Gestor** e outros SaaS do mesmo ecossistema.

Publicável em VPS com **Easypanel** (Docker), sem dependência de interface gráfica ou Windows.

---

## Requisitos Funcionais (RF)

| ID | Descrição |
|----|-----------|
| RF01 | Emitir NF-e 4.0 |
| RF02 | Cancelar NF-e |
| RF03 | Emitir Carta de Correção Eletrônica (CC-e) |
| RF04 | Consultar situação de NF-e na SEFAZ |
| RF05 | Inutilizar faixa de numeração NF-e |
| RF06 | Consultar status do serviço SEFAZ |
| RF07 | Emitir NFC-e 4.0 |
| RF08 | Cancelar NFC-e |
| RF09 | Emitir CT-e 4.0 |
| RF10 | Cancelar CT-e |
| RF11 | Emitir MDF-e 3.0 |
| RF12 | Encerrar MDF-e |
| RF13 | Cancelar MDF-e |
| RF14 | Gerar DANFE NF-e em PDF (base64) |
| RF15 | Gerar DANFE NFC-e (cupom) em PDF (base64) |
| RF16 | Validar certificado A1 (.pfx) |
| RF17 | Upload de certificado A1 |
| RF18 | Controle de numeração sequencial por CNPJ/modelo/série (com lock atômico) |
| RF19 | Health check com verificação de conectividade PostgreSQL |

## Requisitos Não-Funcionais (RNF)

| ID | Descrição |
|----|-----------|
| RNF01 | Autenticação via API Key (header `X-Api-Key`) |
| RNF02 | Docker Linux — sem GDI+/System.Drawing Win32 |
| RNF03 | DANFE sem FastReport (usa `NFe.Danfe.Nativo` + `PdfSharpCore`) |
| RNF04 | Logs estruturados via Serilog (console + arquivo rotativo) |
| RNF05 | Migrations automáticas no startup |
| RNF06 | Timeout configurável para chamadas SEFAZ (padrão 30s) |
| RNF07 | XMLs autorizados salvos em volume persistente |
| RNF08 | Serviços de emissão como Transient (DFe.NET não é thread-safe) |
| RNF09 | Lock pessimista (`SELECT FOR UPDATE`) para numeração sequencial |

---

## Arquitetura

```
HTTP Request
    │
    ▼
ApiKeyMiddleware (401 se ausente/inválida)
    │
    ▼
Controllers (thin — só valida entrada e chama service)
    │
    ▼
Services (orquestra DFe.NET + AppDbContext)
    ├── NFeService
    ├── NFCeService
    ├── CTeService
    ├── MDFeService
    ├── DanfeService
    ├── NumeracaoService
    └── CertificadoService
    │
    ├── DFe.NET (Zeus.Net.NFe.NFCe, Zeus.Net.CTe, Zeus.Net.MDFe)
    │       └── SEFAZ (SOAP/HTTPS)
    │
    └── AppDbContext (PostgreSQL via Npgsql/EF Core)
            ├── EmissaoLog
            └── NumeracaoSequencial
```

---

## Stack

| Camada | Tecnologia |
|--------|-----------|
| Framework | ASP.NET Core 8 (Controllers) |
| Emissão Fiscal | DFe.NET — ZeusAutomacao (`Zeus.Net.NFe.NFCe`, `Zeus.Net.CTe`, `Zeus.Net.MDFe`) |
| DANFE PDF | `NFe.Danfe.Nativo` + `PdfSharpCore` (Linux-safe) |
| ORM | EF Core 8 + Npgsql |
| Banco | PostgreSQL 16 |
| Logs | Serilog (Console + File rolling) |
| Health Checks | `AspNetCore.HealthChecks.NpgSql` |
| Container | Docker (`mcr.microsoft.com/dotnet/aspnet:8.0`) |
| Deploy | Easypanel (Docker Compose) |
| Config | `appsettings.json` + variáveis de ambiente |

---

## Estrutura de Arquivos

```
FiscalService/
├── src/
│   └── FiscalService.Api/
│       ├── Controllers/          ← HTTP layer (8 controllers)
│       ├── Services/             ← Lógica de negócio / orquestração DFe.NET
│       ├── Models/
│       │   ├── Requests/         ← DTOs de entrada
│       │   └── Responses/        ← DTOs de saída
│       ├── Data/
│       │   ├── AppDbContext.cs
│       │   └── Entities/         ← EmissaoLog, NumeracaoSequencial
│       ├── Middlewares/          ← ApiKeyMiddleware
│       ├── Config/               ← FiscalConfig (bind appsettings)
│       ├── Helpers/              ← UfHelper (UF → CodigoUfIbge)
│       ├── Schemas/              ← XSDs do DFe.NET
│       ├── Program.cs
│       ├── appsettings.json
│       └── Dockerfile
├── docker-compose.yml
├── .env.example
├── PLANNING.md
├── PROGRESS.md
└── README.md
```

---

## Fases

### Fase 1 — Core NF-e (atual)
- Setup projeto, NuGets, Serilog, EF Core + PostgreSQL
- Middleware autenticação, FiscalConfig, UfHelper
- NFeService completo (emitir, cancelar, CC-e, consultar, inutilizar, status)
- DanfeService (NF-e + NFC-e)
- Docker + docker-compose

### Fase 2 — NFC-e / CT-e / MDF-e
- NFCeService, CTeService, MDFeService
- NumeracaoService com lock pessimista
- CertificadoService

### Fase 3 — Melhorias
- Swagger/OpenAPI completo com exemplos
- Endpoint de consulta de logs de emissão
- Suporte a contingência SVC-AN / SVC-RS
- Endpoints assíncronos com callback webhook
- Retry automático em falha de conectividade SEFAZ

---

## Riscos

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| DFe.NET não thread-safe | Alto | Serviços como Transient |
| Certificado .pfx expirado | Alto | Validação no upload + alerta no health check |
| SEFAZ fora do ar | Médio | Timeout configurável + resposta de erro padronizada |
| Reforma tributária (Split Payment) | Médio | Manter DFe.NET sempre na versão mais recente |
| GDI+ / System.Drawing no Linux | Alto | NFe.Danfe.Nativo + PdfSharpCore (sem GDI+) |

---

## Critérios de Sucesso

- [ ] NF-e emitida com cStat 100 em homologação
- [ ] DANFE gerado em PDF no Linux sem erros
- [ ] Container Docker sobe e responde `/health` com status `healthy`
- [ ] Migrations aplicadas automaticamente no startup
- [ ] Numeração sequencial sem duplicidade sob carga concorrente
