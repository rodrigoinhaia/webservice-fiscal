# DANFE / PDF — estratégia multiplataforma

## Situação atual

- A API expõe `POST /api/danfe/nfe` e `POST /api/danfe/nfce`, retornando `DanfeResponse` com `pdfBase64` em caso de sucesso.
- **`POST /api/danfe/nfe/html`** e **`POST /api/danfe/nfce/html`** retornam HTML imprimível (`DanfeHtmlResponse`), funcional em **Linux** e Windows. Com query **`inline=true`**, a resposta é `text/html` para abrir no navegador e usar **Ctrl+P** (salvar em PDF fica a cargo do cliente).
- `DanfeService` em PDF hoje **não gera PDF** em Linux: lança `NotSupportedException` com orientação a substituir por biblioteca compatível (o layout legado NFe.Danfe.Nativo é Windows-centric).

## Contrato da API (estável)

### PDF

| Campo | Significado |
|--------|----------------|
| `sucesso` | `true` apenas se o PDF foi gerado. |
| `pdfBase64` | Conteúdo do PDF em Base64 (vazio se falhou). |
| `erro.tipo` | `NaoSuportado` quando a implementação não está disponível no host; `ErroInterno` para falhas inesperadas. |
| `erro.mensagem` / `erro.detalhe` | Mensagem amigável e detalhe técnico (ex.: exceção). |

Clientes devem tratar `sucesso: false` + `NaoSuportado` como “serviço de DANFE PDF não configurado”, não como erro de NF-e.

### HTML

| Campo / parâmetro | Significado |
|-------------------|-------------|
| `sucesso` | `true` se o HTML foi montado. |
| `html` | Documento HTML completo (UTF-8), com CSS de impressão básico. |
| `?inline=true` | Resposta `Content-Type: text/html` em vez de JSON. |
| `erro` | XML inválido ou falha inesperada (`ErroInterno`). |

O HTML gerado cobre os blocos principais do `nfeProc` (identificação, emitente/destinatário com endereço,
itens com NCM/CFOP e resumo de impostos, totais, cobrança/duplicatas, transporte, pagamentos, infAdic,
protocolo). **Não** equivale ao PDF oficial do MOC em todos os detalhes gráficos e campos opcionais raros.

## Opções de implementação (recomendações)

1. **HTML + navegador (implementado)** — atende Linux imediatamente; PDF via impressão do browser.
2. **Serviço externo** — microserviço ou fila que recebe XML `nfeProc` e devolve PDF (ex.: worker Windows, QuestPDF, IronPDF licenciado, etc.). A API REST permanece como fachada.
3. **Biblioteca .NET cross-platform** — integrar pacote que renderize DANFE a partir do XML oficial (ex.: comunidade QuestPDF / templates MOC). Manter geração **fora** do hot path de emissão quando possível (assíncrono).
4. **Cliente rico** — ERP gera PDF/HTML localmente com o `xmlAutorizado` retornado pela emissão; endpoints DANFE opcionais.

## Alinhamento DFe.NET

- O XML autorizado segue o **nfeProc** esperado pela documentação SEFAZ / DFe.NET.
- Qualquer renderizador deve respeitar o **leiaute** e regras de homologação vs produção (incluindo “SEM VALOR FISCAL” em homologação).

## Próximos passos técnicos no repositório

- Substituir o corpo de `DanfeService.GerarNFePdf` / `GerarNFCePdf` por implementação cross-platform (PDF).
- Opcional: ampliar `DanfeHtmlRenderer` para lacres em `vol`, múltiplos `reboque`, grupos opcionais raros do leiaute.
- Opcional: feature flag `Fiscal:DanfeHabilitado` para não expor rotas em ambientes sem motor de PDF.
