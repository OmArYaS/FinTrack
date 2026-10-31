import type { ApiError } from './models';

/** Stable ProblemDetails `code` values raised by the Application layer (docs/architecture.md). */
const KNOWN_CODES: Record<string, string> = {
  SELF_TRANSFER: 'Source and destination accounts must be different.',
  INVALID_AMOUNT: 'Amount must be greater than zero.',
  CURRENCY_MISMATCH: 'Both accounts must use the same currency.',
  EXCEEDS_TRANSACTION_LIMIT: 'Amount exceeds the per-transaction limit.',
  EXCEEDS_DAILY_LIMIT: 'The daily transfer limit for this account has been reached.',
  INSUFFICIENT_FUNDS: 'Not enough funds available in the source account.',
  ACCOUNT_CLOSED: 'That account is closed and cannot be used.',
  ACCOUNT_FROZEN: 'That account is frozen. Contact your relationship manager.',
  CONCURRENT_UPDATE: 'The account was modified by someone else. Review balances and retry.',
  CUSTOMER_EXISTS: 'A customer with this id or email already exists.',
  ACCOUNT_NOT_FOUND: 'The account could not be found.',
  CUSTOMER_REQUIRED: 'Select a customer first.',
  INVALID_ACCOUNT_TYPE: 'Unknown account type.',
  INVALID_CURRENCY: 'Currency must be a 3-letter ISO-4217 code.',
  ALREADY_CLOSED: 'That account is already closed.',
  BALANCE_NOT_ZERO: 'Only zero-balanced accounts can be closed.',
  NUMBER_GENERATION: 'Could not allocate an account number. Please retry.',
};

/** Maps a thrown service error ({ http, ...problem }) to a user-facing sentence. */
export function friendlyProblem(error: unknown): string {
  const problem = error as Partial<ApiError> | null;
  if (problem && typeof problem.code === 'string' && KNOWN_CODES[problem.code]) {
    return KNOWN_CODES[problem.code]!;
  }
  if (problem?.detail) return problem.detail;
  if (problem?.http === 403) return 'You are not allowed to perform this action.';
  if (problem?.http === 404) return 'The requested resource could not be found.';
  if (problem?.http === 401) return 'Session expired. Please sign in again.';
  return 'Something went wrong. Please try again.';
}
