---
name: dotnet-fiscal-dfe
description: >-
  Guides fullstack .NET (ASP.NET Core) development for Brazilian electronic fiscal
  documents (NFe, NFCe) using ZeusAutomacao/DFe.NET, certificates, SEFAZ SOAP, and
  clean layering. Use when working on fiscal emission, DFe, NFe, NFCe, SEFAZ,
  certificado A1, XML assinado, eventos, lote, or when the user mentions DFe.NET,
  webservice-fiscal, or integração fiscal Brasil.
---

# .NET fullstack e emissão fiscal (DFe.NET)

## Prioridade

1. Tratar **[ZeusAutomacao/DFe.NET](https://github.com/ZeusAutomacao/DFe.NET)** como referência de contrato: layouts, assinatura, validação de schema, serviços SEFAZ.
2. Antes de reimplementar geração de XML, assinatura, ou SOAP, alinhar nomes de classes e fluxos ao que a biblioteca expõe.
3. Conferir **Notas Técnicas** e **MOC** oficiais; se manual e lib divergirem, checar issues/PRs do DFe.NET e a documentação do pacote NuGet usado no `csproj`.

## Arquitetura (ASP.NET Core)

- **API (controllers / minimal APIs)** → **aplicação / serviços fiscais** → **domínio (entidades, status, chave 44)** → **infraestrutura (HTTP/SOAP, certificados, DB, filas)**.
- Serviços fiscais orquestram regras: **não** colocar regra pesada em controllers; **não** SQL cru na API para fluxo de negócio fiscal.
- Configuração sensível (PFX, URLs homologação/produção) via `IConfiguration`, user secrets, Key Vault — **nunca** em código versionado.

## Certificados e SEFAZ

- Certificado A1: carregar de forma segura (arquivo/PFX fora do repositório; documentar thumbprint, store ou HSM conforme o padrão do projeto).
- Deixar explícito o **ambiente** (homologação × produção) e validar transição: URLs, `cUF`, série, numeração.
- Para padrões de **mTLS, SOAP, distribuição, manifestação, XMLDSig**, pode inspirar processo (outra stack) em [raphahgomes/monitor-fiscal-sefaz](https://github.com/raphahgomes/monitor-fiscal-sefaz); implementação continua em C# e DFe.NET.

## Ecossistema de consulta

Repositórios de apoio (modelagem, agregados, visão ampla) estão resumidos em [reference.md](reference.md). A implementação neste repo permanece idiomática em **.NET** e alinhada ao **DFe.NET** no escopo NFe/NFCe.

## Código e qualidade

- C# moderno, nullable, `async/await` em I/O; erros fiscais com contexto (`cStat`, `xMotivo`, lote) quando fizer sentido.
- Testes: unitário em serviços puros; integração com mock SEFAZ ou XMLs de homologação, quando o projeto tiver padrão.
- Não adicionar pacotes fiscais novos no `.csproj` sem alinhar com o time.

## Front (fullstack no mesmo repositório)

- **Backend** é fonte de verdade para regras fiscais; UI coleta, exibe e dispara operações. Não duplicar cálculo de impostos ou validação crítica só no client.

## Alinhamento com regras do projeto

Se existir **`.cursor/rules/dotnet-fiscal-dfe.mdc`**, manter o mesmo eixo: DFe.NET como eixo, camadas, certificados e tabela de repositórios de apoio.
