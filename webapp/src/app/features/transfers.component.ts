import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { AccountService } from '../core/account.service';
import { TransferService } from '../core/transfer.service';
import { money } from '../core/format';
import { friendlyProblem } from '../core/api-messages';
import type { AccountSummary, ApiError, TransferResult } from '../core/models';

/**
 * Internal transfer entry form. All bank rules (limits, funds, currency, status)
 * live in the Application layer; the UI only shapes input and maps codes to copy.
 */
@Component({
  selector: 'app-transfers',
  standalone: true,
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <h1>Transfer funds</h1>
      <p>Move money between accounts — retail callers move between their own accounts, staff between any two.</p>
    </header>

    @if (failed(); as msg) {
      <div class="global-error">{{ msg }}</div>
    }

    @if (success(); as r) {
      <div class="card receipt">
        <h2>Transfer complete</h2>
        <p>
          Reference <span class="mono">{{ r.transferReference }}</span>
          — posted to both ledgers atomically.
        </p>
        <div class="btn-row">
          <button class="btn" (click)="startAgain()">Make another transfer</button>
        </div>
      </div>
    } @else {
      <form [formGroup]="form" (ngSubmit)="submit()" class="form-grid card">
        <label>From account
          <select formControlName="fromAccountId">
            <option [value]="''" disabled>Source account…</option>
            @for (a of accounts(); track a.id) {
              <option [value]="a.id">{{ a.number }} · {{ money(a.balance, a.currency) }} ({{ a.accountType }})</option>
            }
          </select>
          @if (showError('fromAccountId')) {
            <div class="field-error">Pick the source account.</div>
          }
        </label>

        <label>To account
          <select formControlName="toAccountId">
            <option [value]="''" disabled>Destination account…</option>
            @for (a of accounts(); track a.id) {
              <option [value]="a.id">{{ a.number }} · {{ a.accountType }}</option>
            }
          </select>
          @if (showError('toAccountId')) {
            <div class="field-error">Pick the destination account.</div>
          }
        </label>

        <label>Amount ({{ currencyOf('fromAccountId') }})
          <input type="number" step="0.01" min="0.01" formControlName="amount" placeholder="e.g. 250.00" />
          @if (showError('amount')) {
            <div class="field-error">Enter an amount above zero.</div>
          }
        </label>

        <label>Reference / description
          <input formControlName="description" maxlength="140" placeholder="e.g. Rent share" />
          <div class="muted" style="font-size: .76rem">Optional, stored on both ledger legs.</div>
        </label>

        <div class="btn-row full">
          <button class="btn" type="submit" [disabled]="submitting() || form.invalid">
            {{ submitting() ? 'Posting…' : 'Post transfer' }}
          </button>
          <span class="muted" style="font-size: .8rem">
            Limits: 25,000 per transaction · 100,000 daily outflow per account.
          </span>
        </div>
      </form>

      @if (accounts().length === 0 && !loading()) {
        <div class="card empty mt">No selectable accounts for this profile.</div>
      }
    }
  `,
})
export class TransfersComponent {
  private readonly api = inject(AccountService);
  private readonly transfers = inject(TransferService);

  protected readonly money = money;

  readonly accounts = signal<AccountSummary[]>([]);
  readonly loading = signal(true);
  readonly failed = signal<string | null>(null);
  readonly success = signal<TransferResult | null>(null);
  readonly submitting = signal(false);
  readonly attempted = signal(false);

  readonly form = this.fb.group({
    fromAccountId: ['', Validators.required],
    toAccountId: ['', Validators.required],
    amount: ['', [Validators.required, Validators.min(0.01)]],
    description: [''],
  });

  constructor() {
    this.api.list().subscribe({
      next: accounts => {
        this.accounts.set(accounts);
        this.loading.set(false);
      },
      error: (err: ApiError) => {
        this.failed.set(friendlyProblem(err));
        this.loading.set(false);
      },
    });
  }

  /** Validation copy appears only after first submit — deterministic with OnPush. */
  showError(control: 'fromAccountId' | 'toAccountId' | 'amount'): boolean {
    return this.attempted() && this.form.controls[control].invalid;
  }

  currencyOf(controlName: 'fromAccountId' | 'toAccountId'): string {
    const id = this.form.controls[controlName].value;
    return this.accounts().find(a => a.id === id)?.currency ?? 'EGP';
  }

  async submit(): Promise<void> {
    this.attempted.set(true);
    if (this.form.invalid) return;

    const { fromAccountId, toAccountId, amount, description } = this.form.getRawValue();
    if (fromAccountId === toAccountId) {
      this.failed.set('Source and destination must be different accounts.');
      return;
    }

    this.submitting.set(true);
    this.failed.set(null);
    try {
      const result = await firstValueFrom(this.transfers.execute({
        fromAccountId,
        toAccountId,
        amount: Number(amount),
        description: description.trim() ? description.trim() : null,
      }));
      this.success.set(result);
      this.refreshBalances(); // posting changed both legs — refetch so the next form is accurate
    } catch (err) {
      this.failed.set(friendlyProblem(err));
    } finally {
      this.submitting.set(false);
    }
  }

  startAgain(): void {
    this.success.set(null);
    this.failed.set(null);
    this.attempted.set(false);
    this.form.reset({ fromAccountId: '', toAccountId: '', amount: '', description: '' });
  }

  private refreshBalances(): void {
    this.api.list().subscribe({
      next: accounts => this.accounts.set(accounts),
      error: () => { /* receipt already closed the interaction; next visit refreshes */ },
    });
  }
}
