# Smoke test — homologação

Checklist manual após deploy ou alteração relevante no `FiscalService.Api`. Execute sempre em **homologação** antes de produção.

## Pré-condições

- API no ar com `ASPNETCORE_ENVIRONMENT` adequado (ex.: Development para Swagger).
- Variáveis: `ApiKey`, `Database__ConnectionString` (ou `.env` com `DB_PASSWORD` / URL).
- PostgreSQL acessível; migrations aplicadas (`/health` com `banco: healthy`).
- Certificado A1 válido para **homologação** e schemas em `FiscalService.Api/Schemas`.
- `Fiscal__Ambiente=Homologacao` (ou equivalente no corpo do emitente).

## 1. Saúde

```http
GET /health
```

Esperado: HTTP 200, JSON com `status: healthy` (ou degradado só se dependência opcional falhar).

## 2. Autenticação

```http
GET /api/nfe/status-sefaz
```

Sem header `X-Api-Key`: **401**. Com chave correta: **200** e payload JSON.

## 3. NFC-e (QR + CSC)

- Emitir NFC-e com `idCsc` (numérico, ex.: `"1"` ou `"000001"` — a biblioteca normaliza), `csc` e, se a UF exigir, `qrCodeVersao` (`"1"`, `"2"` ou `"3"`; padrão no API é `"2"`).
- Verificar no XML retornado (ou no log salvo) presença de `infNFeSupl` com `qrCode` e `urlChave` preenchidos após autorização.

## 4. NF-e / CT-e / MDF-e (opcional por escopo)

- **NF-e:** emitir nota de teste homologação; cancelar ou inutilizar faixa de teste conforme política da SEFAZ.
- **CT-e / MDF-e:** uma emissão de teste + evento de cancelamento/encerramento se aplicável.

## 5. Regressão rápida

- Numeração: `GET /api/numeracao/{cnpj}/{modelo}/{serie}` retorna próximo número.
- Certificado: `POST /api/certificado/validar` com path/senha de teste.

## Critério de “passou”

Nenhum 5xx inesperado; SEFAZ retorna `cStat` de sucesso nos fluxos testados; logs sem stack trace de configuração (connection string, certificado, schemas).
