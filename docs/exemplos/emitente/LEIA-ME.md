# Cadastro de emitente — homologação

## Arquivos

| Arquivo | Uso |
|---------|-----|
| `cadastro-homologacao.json` | Modelo para `POST /api/emitentes` |
| `cadastro-homologacao.template.json` | Mesmo conteúdo com placeholders explícitos |

## O que ajustar antes do smoke

1. **cnpj** — 14 dígitos do certificado A1 de homologação.
2. **certificadoPath** — nome do `.pfx` em `certificados/` (ex.: `empresa-homologacao.pfx`).
3. **certificadoSenha** — senha do PFX (no cadastro via API; depois fica criptografada no banco).
4. **uf**, **codigoMunicipio**, **ie** — dados reais do emitente de teste.
5. **crt** — `1` ou `2` Simples; `3` Lucro Presumido/Real.
6. **ambiente** — sempre `Homologacao` até ir para produção.

Coloque o arquivo `.pfx` em:

- Docker: volume `/app/certificados`
- Local: `src/FiscalService.Api/certificados/` (ou path em `Fiscal:DiretorioCertificados`)

## Upload alternativo

```bash
curl -X POST http://localhost:5555/api/certificado/upload-arquivo \
  -H "X-Api-Key: SUA_CHAVE" \
  -F "arquivo=@C:\caminho\empresa.pfx" \
  -F "senha=SENHA" \
  -F "nome=empresa-homologacao.pfx"
```

Depois use `"certificadoPath": "empresa-homologacao.pfx"` no JSON de cadastro.
