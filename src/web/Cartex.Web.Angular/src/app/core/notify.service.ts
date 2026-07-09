import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable({ providedIn: 'root' })
export class NotifyService {
  private readonly snack = inject(MatSnackBar);

  success(message: string): void {
    this.snack.open(message, undefined, { duration: 2500 });
  }

  error(err: unknown): void {
    this.snack.open(describe(err), 'OK', { duration: 5000 });
  }
}

function describe(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error;
    if (body && typeof body === 'object') {
      if (typeof body.detail === 'string' && body.detail) return body.detail;
      if (typeof body.title === 'string' && body.title) return body.title;
    }
    if (err.status === 0) return 'Server bilan aloqa yo‘q';
    return `Xatolik (${err.status})`;
  }
  return String(err);
}
