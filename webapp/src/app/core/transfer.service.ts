import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';

import type { TransferResult } from './models';
import { problemOf } from './account.service';

export interface TransferRequest {
  fromAccountId: string;
  toAccountId: string;
  amount: number;
  description: string | null;
}

@Injectable({ providedIn: 'root' })
export class TransferService {
  private readonly http = inject(HttpClient);

  execute(request: TransferRequest): Observable<TransferResult> {
    return this.http.post<TransferResult>('/api/transfers', request)
      .pipe(catchError(err => {
        const problem = problemOf(err as import('@angular/common/http').HttpErrorResponse);
        return throwError(() => ({ http: (err as import('@angular/common/http').HttpErrorResponse).status, ...problem }));
      }));
  }
}
