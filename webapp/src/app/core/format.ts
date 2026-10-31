/** Presentation helpers kept free of Angular so they are unit-testable anywhere. */

const formatters = new Map<string, Intl.NumberFormat>();

/** Money display honouring the account's currency (e.g. 1 234.50 → "EGP 1,234.50"). */
export function money(amount: number, currency = 'EGP'): string {
  const key = `${currency}-v1`;
  let fmt = formatters.get(key);
  if (!fmt) {
    fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency, currencyDisplay: 'code' });
    formatters.set(key, fmt);
  }
  return fmt.format(amount);
}

/** Signed LedgerEntry display: −EGP 20,000.00 for debits, +EGP 500.00 for credits. */
export function signed(amount: number, currency = 'EGP'): string {
  return `${amount < 0 ? '−' : '+'}${money(Math.abs(amount), currency)}`;
}

/** Stable string for ISO timestamps in tables. */
export function shortDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' });
}

/** Only date portion for list views. */
export function shortDay(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString('en-GB', { dateStyle: 'medium' });
}
