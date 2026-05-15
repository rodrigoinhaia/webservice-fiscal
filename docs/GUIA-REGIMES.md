# Guia de regimes tributários — NF-e / NFC-e

## Quem calcula o quê

| Papel | Responsabilidade |
|-------|------------------|
| **ERP / sistema de origem** | CRT, CST/CSOSN, bases, alíquotas, valores de ICMS, ST, IPI, PIS, COFINS, DIFAL, numeração fiscal |
| **FiscalService.Api** | Validar combinações suportadas, montar XML 4.0, assinar, comunicar com SEFAZ, persistir log |

O webservice **não** substitui o motor tributário do ERP.

## CRT na NF-e

| CRT | Regime | Campo no item |
|-----|--------|----------------|
| 1 | Simples Nacional | `csosnIcms` |
| 2 | Simples (excesso sublimite) | `csosnIcms` |
| 3 | Regime normal (LP e LR) | `cstIcms` |

Lucro Presumido e Lucro Real usam o mesmo **CRT 3**; a diferença está nos CST e valores calculados pelo ERP.

## CST ICMS suportados (CRT 3)

`00`, `10`, `20`, `30`, `40`, `41`, `50`, `51`, `60`, `70`, `90`

CST não listado → HTTP 422 `TributacaoInvalida` (sem fallback silencioso).

## CSOSN suportados (CRT 1/2)

`101`, `102`, `103`, `201`, `202`, `203`, `500`, `900` (padrão `102` se omitido).

## PIS / COFINS

| CST | Grupo XML |
|-----|-----------|
| 01, 02 | Alíquota |
| 03 | Quantidade |
| 04–09 | Não tributado |
| 49, 99 | Outros |

## Emissão recomendada

1. `POST /api/emitentes` — cadastrar emitente + certificado A1  
2. `POST /api/nfe/emitir` com `emitenteCnpj` — ver `docs/exemplos/nfe/emitir-via-emitente-cnpj.json`

## Referência DFe.NET (AppTeste)

| Operação API | Referência conceitual |
|--------------|----------------------|
| Emitir NF-e | `NFe.AppTeste` — montagem `NFe.Classes.NFe` + `ServicosNFe.NFeAutorizacao` |
| Cancelar / CC-e | `RecepcaoEventoCancelamento`, `RecepcaoEventoCartaCorrecao` |
| Distribuição DF-e | `NfeDistDFeInteresse` |
| Manifestação | `RecepcaoEventoManifestacaoDestinatario` |

Repositório: https://github.com/ZeusAutomacao/DFe.NET

## Documentos relacionados

- [`TRIBUTACAO-MATRIZ.md`](TRIBUTACAO-MATRIZ.md) — tabela CST × classe C#
- [`exemplos/README.md`](exemplos/README.md) — payloads JSON
- [`SMOKE-HOMOLOGACAO.md`](SMOKE-HOMOLOGACAO.md) — checklist homologação
