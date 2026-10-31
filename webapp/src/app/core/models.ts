/** API contract mirrors (camelCase wire format from ASP.NET Core). */

export type AccountStatus = 'Active' | 'Frozen' | 'Closed';
export type AccountType = 'Checking' | 'Savings' | 'FixedDeposit';
export type TransactionType = 'Opening' | 'Deposit' | 'Withdrawal' | 'TransferIn' | 'TransferOut' | 'Fee';

export interface AccountSummary {
  id: string;
  customerId: string;
  number: string;
  accountType: AccountType;
  status: AccountStatus;
  currency: string;
  balance: number;
  openedAt: string;
}

export interface LedgerEntry {
  transactionId: string;
  type: TransactionType;
  amount: number;
  balanceAfter: number;
  reference: string;
  description: string | null;
  createdAt: string;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Customer {
  id: string;
  fullName: string;
  email: string;
  externalId: string;
}

export interface TransferResult {
  transferReference: string;
  fromBalanceAfter: number;
  toBalanceAfter: number;
}

/** RFC 9457 ProblemDetails shape the API returns with the stable `code` field. */
export interface ApiProblem {
  title?: string;
  status?: number;
  code?: string;
  detail?: string;
}

/** What the data services actually throw: the ProblemDetails plus the HTTP status. */
export type ApiError = ApiProblem & { http: number };
