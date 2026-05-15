# Exemplos de payload — FiscalService API

JSON prontos para `POST /api/nfe/emitir` e correlatos. Ajuste CNPJ, certificado e valores antes de enviar à SEFAZ.

| Arquivo | CRT | Cenário |
|---------|-----|---------|
| [nfe/crt1-simples-csosn102-homologacao.json](nfe/crt1-simples-csosn102-homologacao.json) | 1 | Simples Nacional — CSOSN 102 |
| [nfe/crt3-lucro-presumido-icms00.json](nfe/crt3-lucro-presumido-icms00.json) | 3 | Regime normal — CST 00 (LP) |
| [nfe/crt3-lucro-real-icms10-st.json](nfe/crt3-lucro-real-icms10-st.json) | 3 | Regime normal — CST 10 + ST (LR) |
| [nfe/crt3-icms20-reducao-base.json](nfe/crt3-icms20-reducao-base.json) | 3 | CST 20 — redução de base |
| [nfe/crt3-item-com-ipi.json](nfe/crt3-item-com-ipi.json) | 3 | Item com IPI tributado |
| [nfe/crt3-interestadual-difal.json](nfe/crt3-interestadual-difal.json) | 3 | Interestadual com DIFAL (`ICMSUFDest`) |
| [nfe/emitir-via-emitente-cnpj.json](nfe/emitir-via-emitente-cnpj.json) | — | Emissão só com `emitenteCnpj` (cadastro prévio) |
| [nfe/contingencia-svc-an.json](nfe/contingencia-svc-an.json) | — | Contingência SVC-AN |

### Emitente (cadastro)

| Arquivo | Uso |
|---------|-----|
| [emitente/cadastro-homologacao.json](emitente/cadastro-homologacao.json) | `POST /api/emitentes` |

Matriz CST/CSOSN: [`../TRIBUTACAO-MATRIZ.md`](../TRIBUTACAO-MATRIZ.md).  
Referência DFe.NET: [ZeusAutomacao/DFe.NET](https://github.com/ZeusAutomacao/DFe.NET).
