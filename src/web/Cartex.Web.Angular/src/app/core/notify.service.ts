import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoService } from '@jsverse/transloco';

@Injectable({ providedIn: 'root' })
export class NotifyService {
  private readonly snack = inject(MatSnackBar);
  private readonly transloco = inject(TranslocoService);

  success(message: string): void {
    this.snack.open(message, undefined, {
      duration: 2500,
      horizontalPosition: 'right',
      verticalPosition: 'top',
      panelClass: 'cx-toast-ok',
    });
  }

  error(err: unknown): void {
    this.snack.open(this.describe(err), 'OK', {
      duration: 5000,
      horizontalPosition: 'right',
      verticalPosition: 'top',
      panelClass: 'cx-toast-err',
    });
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 0) return this.transloco.translate('err_server_unreachable');
      if (err.status === 403) return this.transloco.translate('err_forbidden');
      if (err.status >= 500) return this.transloco.translate('err_server_error');
      const body = err.error;
      if (body && typeof body === 'object') {
        if (typeof body.detail === 'string' && body.detail) return body.detail;
        if (typeof body.title === 'string' && body.title) return body.title;
      }
      return this.transloco.translate('err_server_error');
    }
    return String(err);
  }
}
