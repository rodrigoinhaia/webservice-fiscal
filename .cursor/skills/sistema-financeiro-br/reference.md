# Referência — tipos e schemas (ponto de partida)

Adaptar nomes de arquivo (`AccountPayable.ts` vs módulo único) ao padrão do repositório. Ajustar validação de CPF/CNPJ ao padrão já usado no projeto.

## Tipos (exemplos)

```ts
// AccountPayable
type AccountPayable = {
  id: string
  organizationId: string
  supplier: string
  cnpj: string
  documentNumber: string
  dueDate: Date
  amount: number
  paidAmount?: number
  status: 'open' | 'paid' | 'partially_paid' | 'overdue'
  paymentMethod?: 'bank_transfer' | 'cash' | 'credit_card' | 'cheque'
  paymentDate?: Date
  accountId: string
  createdAt: Date
  updatedAt: Date
}

// AccountReceivable
type AccountReceivable = {
  id: string
  organizationId: string
  customer: string
  cpfCnpj?: string
  documentNumber: string
  dueDate: Date
  amount: number
  receivedAmount?: number
  status: 'open' | 'received' | 'partially_received' | 'overdue'
  receivingDate?: Date
  accountId: string
  createdAt: Date
  updatedAt: Date
}

// AccountEntry
type AccountEntry = {
  id: string
  organizationId: string
  chartAccountId: string
  date: Date
  type: 'debit' | 'credit'
  amount: number
  description: string
  relatedEntityId: string
  relatedEntityType: 'account_payable' | 'account_receivable' | 'manual'
  createdAt: Date
}

// ChartAccount
type ChartAccount = {
  id: string
  organizationId: string
  code: string
  name: string
  type: 'asset' | 'liability' | 'equity' | 'revenue' | 'expense'
}

// FiscalDocument
type FiscalDocument = {
  id: string
  organizationId: string
  type: 'NFCe' | 'NFe'
  accessKey: string
  authorizationStatus: 'authorized' | 'denied' | 'pending' | 'error'
  authorizationDate?: Date
  customerCpfCnpj?: string
  amount: number
  xmlPath: string
  createdAt: Date
  updatedAt: Date
}
```

## Zod — nomes sugeridos

Arquivo sugerido: `lib/validations/AccountsSchemas.ts` (ou `FinancialSchemas.ts`).

- `CreateAccountPayableSchema` / `CreateAccountReceivableSchema`
- `PaymentSchema` (baixa em contas a pagar)
- `ReceiveSchema` (baixa em contas a receber)

Incluir `organizationId` nos schemas **somente** se o padrão da API for exigir no body; caso contrário, usar apenas `session.organizationId` após `requireAuth()`.
