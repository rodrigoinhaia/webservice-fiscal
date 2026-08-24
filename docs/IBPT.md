# Lei 12.741/2012 — IBPT (De Olho no Imposto)

O webservice preenche os **totais aproximados dos tributos** na NF-e e na NFC-e, exigidos pela [Lei 12.741/2012](http://www.planalto.gov.br/ccivil_03/_ato2011-2014/2012/lei/l12741.htm) (transparência fiscal) e pela NT 2013.003 da NF-e.

Portal: [De Olho no Imposto](https://deolhonoimposto.ibpt.org.br/) · API: [Produtos_Get](https://deolhonoimposto.ibpt.org.br/Site/API#!//Produtos_Get)

A lei vale para **todos os regimes**, inclusive **Simples Nacional**: no Simples o DAS não discrimina federal/estadual/municipal no XML da nota, então a carga aproximada do IBPT é o meio usual de cumprir a obrigação no DANFE/cupom.

## O que vai no XML / DANFE

1. **`imposto.vTotTrib`** em cada item (soma federal + estadual + municipal daquele NCM).
2. **`ICMSTot.vTotTrib`** = soma dos itens (rejeição SEFAZ **685** se divergir).
3. **`infAdic.infCpl`**, no rodapé do DANFE/NFC-e:

```text
Totais aproximados dos Tributos cfe. Lei n° 12.741/2012: Federais: R$ X,XX; Estaduais: R$ Y,YY; Municipais: R$ Z,ZZ. Fonte: IBPT/{versão}
```

Se o ERP já enviar `informacoesAdicionais`, o texto da lei é **anexado** (não substitui). Se o texto já contiver `12.741`, nada é duplicado.

## Como o cálculo é feito

Para cada item:

- Base = `valorTotalBruto` (`vProd`).
- Alíquotas IBPT do NCM + UF do emitente + EX (`ncmExcecao`, default 0).
- Federal = alíquota **nacional** se `origemMercadoria` ∈ {0, 3, 4, 5, 8}; **importado** se ∈ {1, 2, 6, 7}.
- `vTotTrib` = federal + estadual + municipal (2 casas, `AwayFromZero`).

## Fontes de dados (nessa ordem)

1. Cache em memória (TTL `Fiscal:Ibpt:CacheMinutos`, padrão 24 h).
2. **Tabela local** CSV/TXT (`Fiscal:Ibpt:ArquivoTabela`) — download no portal (empresa → tabela da UF).
3. **API** `GET https://apidoni.ibpt.org.br/api/v1/produtos` (token + CNPJ + NCM + UF + valor…).

O portal já avisou indisponibilidade da API sem previsão. **Mantenha a tabela local atualizada** (ex.: versão 26.2.A, vigência 20/08/2026–30/09/2026).

Não versionamos a tabela (arquivo grande e de uso restrito ao CNPJ cadastrado). Coloque o CSV em `ibpt/TabelaIBPTax.csv` (gitignored) ou no volume Docker `/app/ibpt`.

## Painel operacional

O FiscalService é **somente API**. Token por CNPJ e upload da tabela CSV ficam no projeto **`webservice-fiscal-painel`** (rotas `/ibpt` e detalhe do emitente). O painel chama, com `X-Api-Key` no BFF:

1. Token do emitente → `PUT /api/emitentes/{cnpj}/ibpt`
2. CSV da UF baixado no portal → `POST /api/ibpt/tabela`

As rotas `/api/*` continuam exigindo `X-Api-Key`. Swagger fica em `/swagger` apenas em Development.

## Token (por CNPJ)

O token é **da empresa no portal IBPT**, não é genérico. Cada CNPJ integrador deve:

1. Criar conta em [deolhonoimposto.ibpt.org.br](https://deolhonoimposto.ibpt.org.br/).
2. Cadastrar a empresa e copiar o token.
3. Enviar no cadastro do emitente **ou** usar um token global só em instância monoempresa.

Prioridade: `configuracaoEmitente.ibptToken` → token salvo no emitente → `Fiscal__Ibpt__Token` / `FISCAL_IBPT_TOKEN`.

O valor **não** é devolvido na API (`possuiIbptToken: true|false`).

```http
PUT /api/emitentes/{cnpj}/ibpt
Content-Type: application/json
X-Api-Key: ...

{ "ibptToken": "cole-o-token-do-portal" }
```

Upload da tabela:

```http
POST /api/ibpt/tabela
Content-Type: multipart/form-data
X-Api-Key: ...

arquivo: TabelaIBPTaxRS.csv
uf: RS
```

## Integração pelos ERPs (Diin Gestor e outros)

Na emissão (`POST /api/nfe/emitir` e `POST /api/nfce/emitir`):

| Campo | Uso |
|---|---|
| `calcularIbpt` | `true`/`false`; omitir = usa `Fiscal:Ibpt:Habilitado` (padrão true) |
| `itens[].ncm` | Obrigatório para o lookup |
| `itens[].ncmExcecao` | EX da tabela (quase sempre 0) |
| `itens[].origemMercadoria` | Define nacional vs importado |
| `itens[].valorAproximadoTributos` | Se o ERP já calculou, ainda assim o webservice **recalcula** quando IBPT está ligado (fonte IBPT). Para usar só o valor do ERP, envie `calcularIbpt: false` |

Consulta avulsa (cadastro de produto no ERP):

```http
GET /api/ibpt/produtos?ncm=19059090&uf=RS&valor=100&cnpj=00000000000000&origemMercadoria=0
X-Api-Key: ...
```

Resposta inclui alíquotas, valores federal/estadual/municipal, `infCpl` pronto e `origemDados` (`api` ou `tabela`).

`GET /api/ibpt/status` — diagnóstico (sem expor o token).

`POST /api/ibpt/tabela/recarregar` — após substituir o CSV no disco.

## Configuração

| Variável | Descrição |
|---|---|
| `FISCAL_IBPT_HABILITADO` | default `true` |
| `FISCAL_IBPT_TOKEN` | fallback global (não commitar) |
| `FISCAL_IBPT_ARQUIVO_TABELA` | path do CSV (Docker: `/app/ibpt/TabelaIBPTax.csv`) |
| `FISCAL_IBPT_UF_TABELA` | UF quando o arquivo não tem coluna UF |
| `Fiscal__Ibpt__Obrigatorio` | `true` = emissão falha se nenhum NCM for resolvido (default `false`) |
| `Fiscal__Ibpt__IncluirInfCpl` | anexa o texto da lei (default `true`) |

## Formato da tabela (CSV `;`)

```text
codigo;ex;tipo;descricao;nacionalfederal;importadosfederal;estadual;municipal;vigenciainicio;vigenciafim;chave;versao;fonte
19059090;0;0;PAO;13,45;18,00;18,00;0,00;20/08/2026;30/09/2026;...;26.2.A;IBPT
```

`tipo` 0 = NCM (usado na NF-e/NFC-e). Tipos 1 (NBS) e 2 (LC 116) são ignorados neste módulo.

## O que o ERP **não** precisa duplicar

Não calcule PIS/COFINS/ICMS “de verdade” a partir do IBPT. Os percentuais IBPT são **estimativa de carga tributária embutida no preço**, informativa. Não alteram `vNF` nem o imposto devido.
