---
name: sistema-financeiro-br
description: >-
  Orienta módulos financeiros brasileiros: contas a pagar/receber, plano de
  contas (ChartAccount), lançamentos (AccountEntry) com partidas dobradas,
  documentos fiscais NFC-e/NF-e, RLS e organizationId. Usar ao implementar AP/AR,
  contabilidade básica BR, integração fiscal SEFAZ, relatórios financeiros ou
  quando o usuário mencionar contas a pagar, receber, plano de contas ou NF-e.
---

# Sistema financeiro BR (unificado)

Skill para **contas a pagar/receber**, **contabilidade básica compatível com práticas BR**, **fiscal (NFC-e / NF-e)** e **segurança por organização (RLS)**. Complementa o skill `feature-completa-fullstack`: toda feature financeira também deve cumprir aquele checklist (API → Service → Repository, testes, `PLANNING.md` / `PROGRESS.md`).

## Objetivo

- Modelar AP/AR com vencimento, status, vínculo ao plano de contas e lançamentos.
- Manter `organizationId` e `setOrganizationContext(orgId)` em todo acesso a dados; nunca confiar em `organizationId` só do body — alinhar com sessão (`requireAuth()` → `session.organizationId`).
- Documentos fiscais: persistir XML, chave de acesso e status de autorização; integração real pode começar em mock/sandbox.
- Entregar testado e documentado como no skill de feature completa.

## Entidades (nomes e papéis)

Definir tipos em `lib/types/` (ajustar nomes de arquivo ao padrão do repo, ex. um arquivo por domínio ou `financial.ts`). Esqueletos TypeScript em [reference.md](reference.md).

| Conceito | Campos-chave |
|----------|----------------|
| **AccountPayable** | `organizationId`, fornecedor, CPF/CNPJ, `documentNumber`, `dueDate`, `amount`, `paidAmount?`, `status` (`open` \| `paid` \| `partially_paid` \| `overdue`), `paymentMethod?`, `paymentDate?`, `accountId` (plano), timestamps |
| **AccountReceivable** | `organizationId`, cliente, `cpfCnpj?`, `documentNumber`, `dueDate`, `amount`, `receivedAmount?`, `status` (`open` \| `received` \| `partially_received` \| `overdue`), `receivingDate?`, `accountId`, timestamps |
| **AccountEntry** | `organizationId`, `chartAccountId`, `date`, `type` (`debit` \| `credit`), `amount`, `description`, `relatedEntityId`, `relatedEntityType` (`account_payable` \| `account_receivable` \| `manual`) |
| **ChartAccount** | `organizationId`, `code` (ex.: 1.1.1), `name`, `type` (`asset` \| `liability` \| `equity` \| `revenue` \| `expense`) |
| **FiscalDocument** | `organizationId`, `type` (`NFCe` \| `NFe`), `accessKey`, `authorizationStatus`, `authorizationDate?`, `customerCpfCnpj?`, `amount`, `xmlPath` (ou storage id), timestamps |

Status **overdue** deve ser derivado por regra de negócio (comparar `dueDate` com “hoje”) ou job — definir no Service, não só no front.

## Validação (Zod)

- Centralizar em `lib/validations/` (ex.: `AccountsSchemas.ts` ou `FinancialSchemas.ts`).
- CPF/CNPJ: validar formato/tamanho aceitável; não fixar só 14 caracteres se o produto aceitar CPF (11) e CNPJ (14) com ou sem máscara — preferir refinamento ou biblioteca já usada no projeto.
- Datas: `z.coerce.date()` ou string ISO + transform, consistente com o restante da API.
- **IDs da organização na API:** preferir validar operações com `session.organizationId` e **não** exigir `organizationId` no body para create, a menos que o padrão do projeto seja outro — evita spoofing.

## Camadas

### Repositories

- `AccountPayableRepository`, `AccountReceivableRepository`, `AccountEntryRepository`, `ChartAccountRepository`, `FiscalDocumentRepository` estendendo `BaseRepository`.
- Sempre `setOrganizationContext(orgId)` antes de qualquer query; filtrar por `organization_id`.
- SQL apenas no repository; sem regra de negócio.

### Services

- **AccountPayableService** / **AccountReceivableService:** criação, baixa parcial/total, atualização de status; orquestrar **AccountEntryService** para refletir movimentos no razão.
- **AccountEntryService:** inserir lançamentos em conjuntos **balanceados** (soma débitos = soma créditos, tolerância mínima para float, ex. 0,01).
- **FinancialReportService:** saldos por conta/período usando repositórios (sem SQL na rota).
- **FiscalDocumentService:** assinar/comunicar (ou mock), persistir XML e metadados; vincular a vendas/AP/AR quando existir regra de negócio.

Contabilidade na criação de título: o exemplo “débito despesa / crédito caixa” na **criação** do payable pode não ser o desejado (caixa só sai no pagamento). Definir no planejamento: lançamentos na **emissão do título** vs no **pagamento**; manter consistência com o produto.

### API Routes

- Padrão do projeto: `requireAuth()`, `organizationId` da sessão, Zod no body/query, delegar ao Service.
- Rotas REST claras (ex.: `app/api/...`) — seguir convenção já existente em `app/api/`.

## Fiscal (NFC-e / NF-e)

- Persistir: XML, protocolo/autorização quando houver, `accessKey`.
- Início: mock ou sandbox SEFAZ; não bloquear fluxo AP/AR por certificado até a fase de hardening.
- Fluxo típico “venda + NF + recebível”: criar **AccountReceivable** (ou pedido) alinhado ao documento fiscal; relacionar `FiscalDocument.id` ou `accessKey` conforme modelo de dados escolhido.

```ts
// Esboço conceitual (adaptar tipos reais)
async createSaleAndInvoice(saleData: SaleData, orgId: string) {
  const receivable = await accountReceivableService.create({ ... })
  const fiscal = await fiscalDocumentService.issueOrStore({ ... })
  return { accountReceivable: receivable, fiscalDocument: fiscal }
}
```

## RLS e segurança

- Toda tabela: `organization_id` + políticas alinhadas ao restante do banco.
- APIs: escopo sempre pela sessão; validar permissões de papel se o módulo for sensível.

## Testes e documentação

- Unit: Services com repositories mockados; regras de saldo, status, overdue.
- Integração: API → Service → Repository (com banco de teste se o projeto usar).
- Atualizar `PLANNING.md` / `PROGRESS.md` com escopo fiscal e contábil.

## Checklist rápido

```
- [ ] Tipos + migrations + RLS
- [ ] Repositories com setOrganizationContext
- [ ] Services: negócio + partidas dobradas onde aplicável
- [ ] Zod + rotas com requireAuth
- [ ] Fiscal: armazenamento XML + chave; sandbox/mock até produção
- [ ] Testes + PLANNING / PROGRESS
```

## Anti-padrões

- `organizationId` vindo só do cliente sem cruzar com a sessão.
- Lançamentos contábeis desbalanceados sem erro explícito.
- Lógica fiscal ou SQL complexo direto na rota.
