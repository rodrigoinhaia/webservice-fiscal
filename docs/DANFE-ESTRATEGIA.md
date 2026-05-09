# DANFE / PDF — estratégia multiplataforma

## Situação atual

- A API expõe `POST /api/danfe/nfe` e `POST /api/danfe/nfce`, retornando `DanfeResponse` com `pdfBase64` em caso de sucesso.
- `DanfeService` hoje **não gera PDF** em Linux: lança `NotSupportedException` com orientação a substituir por biblioteca compatível (o layout legado NFe.Danfe.Nativo é Windows-centric).

## Contrato da API (estável)

| Campo | Significado |
|--------|----------------|
| `sucesso` | `true` apenas se o PDF foi gerado. |
| `pdfBase64` | Conteúdo do PDF em Base64 (vazio se falhou). |
| `erro.tipo` | `NaoSuportado` quando a implementação não está disponível no host; `ErroInterno` para falhas inesperadas. |
| `erro.mensagem` / `erro.detalhe` | Mensagem amigável e detalhe técnico (ex.: exceção). |

Clientes devem tratar `sucesso: false` + `NaoSuportado` como “serviço de DANFE não configurado”, não como erro de NF-e.

## Opções de implementação (recomendações)

1. **Serviço externo** — microserviço ou fila que recebe XML `nfeProc` e devolve PDF (ex.: worker Windows, QuestPDF, IronPDF licenciado, etc.). A API REST permanece como fachada.
2. **Biblioteca .NET cross-platform** — integrar pacote que renderize DANFE a partir do XML oficial (ex.: comunidade QuestPDF / templates MOC). Manter geração **fora** do hot path de emissão quando possível (assíncrono).
3. **Cliente rico** — ERP gera PDF localmente com o `xmlAutorizado` retornado pela emissão; endpoint DANFE opcional.

## Alinhamento DFe.NET

- O XML autorizado segue o **nfeProc** esperado pela documentação SEFAZ / DFe.NET.
- Qualquer renderizador deve respeitar o **leiaute** e regras de homologação vs produção (incluindo “SEM VALOR FISCAL” em homologação).

## Próximos passos técnicos no repositório

- Substituir o corpo de `DanfeService.GerarNFePdf` / `GerarNFCePdf` por implementação escolhida (e testes com XML de homologação).
- Opcional: feature flag `Fiscal:DanfeHabilitado` para não expor rotas em ambientes sem motor de PDF.
