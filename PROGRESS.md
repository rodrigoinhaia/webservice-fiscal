# PROGRESS — FiscalService

## Status Geral

🟢 **Fase 1 e 2 completas** — Projeto estruturado e pronto para compilar/testar.

---

## Fase Atual

**Fase 1+2 concluída** — Todos os serviços implementados, aguardando compilação e testes em homologação.

---

## Métricas

| Métrica | Status |
|---------|--------|
| Cobertura de testes | 🔴 Pendente (foco em testes de integração com certificado real) |
| Build local | 🟡 Requer .NET SDK 8 instalado para validar |
| Docker build | 🟡 Pendente validação |
| Homologação NF-e | 🟡 Pendente certificado de teste SEFAZ |
| DANFE no Linux | 🟡 Pendente validação no container |

---

## Tarefas Concluídas

### Setup e Infraestrutura
- [x] Solução `.sln` e projeto `FiscalService.Api.csproj` criados
- [x] NuGets: EF Core 8, Npgsql, DFe.NET, NFe.Danfe.Nativo, Serilog, HealthChecks
- [x] `appsettings.json` + `appsettings.Production.json` configurados
- [x] `FiscalConfig` POCO com bind de configuração
- [x] `ApiKeyMiddleware` — autenticação por header `X-Api-Key`
- [x] `Program.cs` — Serilog, DI, migrations automáticas, health check

### Banco de Dados
- [x] `AppDbContext` com `EmissaoLog` e `NumeracaoSequencial`
- [x] Índices únicos e compostos configurados via Fluent API

### Serviços Fiscais
- [x] `CertificadoService` — validar e fazer upload de .pfx
- [x] `NumeracaoService` — lock pessimista (`SELECT FOR UPDATE`) PostgreSQL
- [x] `NFeService` — emitir, cancelar, CC-e, consultar, inutilizar, status-sefaz
- [x] `NFCeService` — emitir (CSC/IdCSC), cancelar
- [x] `CTeService` — emitir, cancelar
- [x] `MDFeService` — emitir, encerrar, cancelar
- [x] `DanfeService` — PDF base64 NF-e + NFC-e via `NFe.Danfe.Nativo`
- [x] `UfHelper` — mapeamento de todos os 27 estados para `CodigoUfIbge`

### Controllers (8 controllers)
- [x] `NFeController` — 6 endpoints
- [x] `NFCeController` — 2 endpoints
- [x] `CTeController` — 2 endpoints
- [x] `MDFeController` — 3 endpoints
- [x] `ConsultaController` — 1 endpoint
- [x] `DanfeController` — 2 endpoints
- [x] `CertificadoController` — 2 endpoints
- [x] `NumeracaoController` — 2 endpoints

### DevOps
- [x] `Dockerfile` multi-stage com dependências Linux (PdfSharpCore)
- [x] `docker-compose.yml` com PostgreSQL 16
- [x] `.env.example`
- [x] `.gitignore`

---

## Tarefas Pendentes

- [ ] Instalar .NET SDK 8 e compilar o projeto localmente
- [ ] Executar `dotnet ef migrations add InitialCreate`
- [ ] Baixar schemas XSD do DFe.NET para `src/FiscalService.Api/Schemas/`
- [ ] Obter certificado A1 de homologação SEFAZ
- [ ] Testar emissão NF-e em homologação
- [ ] Validar DANFE no container Linux
- [ ] Configurar pipeline CI/CD (GitHub Actions)
- [ ] Adicionar testes de integração

---

## Próximo Passo Imediato

```bash
# 1. Instalar .NET SDK 8
# Windows: https://dotnet.microsoft.com/download/dotnet/8.0
# Linux: sudo apt install dotnet-sdk-8.0

# 2. Restaurar pacotes e compilar
cd src/FiscalService.Api
dotnet restore
dotnet build

# 3. Criar migration inicial
dotnet ef migrations add InitialCreate

# 4. Baixar schemas XSD
git clone --depth 1 https://github.com/ZeusAutomacao/DFe.NET.git /tmp/dfe
cp -r /tmp/dfe/NFe.AppTeste/Schemas/* src/FiscalService.Api/Schemas/

# 5. Subir com Docker
cp .env.example .env
# editar .env com valores reais
docker-compose up --build
```

---

## Indicadores

| Indicador | Valor Atual | Meta |
|-----------|-------------|------|
| Endpoints implementados | 20/20 | 20 |
| Serviços implementados | 7/7 | 7 |
| Testes unitários | 0 | ≥ 80% cobertura |
| Build passando | 🟡 Não verificado | ✅ |
| SEFAZ homologação | 🔴 Não testado | ✅ |
