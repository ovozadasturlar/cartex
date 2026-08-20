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

  /// Ogohlantirish amalni to'xtatmaydi, shuning uchun tasdiqlash tugmasi bilan va uzoqroq
  /// turadi — kassir uni sezmay o'tib ketmasligi kerak.
  warn(message: string): void {
    this.snack.open(message, 'OK', {
      duration: 8000,
      horizontalPosition: 'right',
      verticalPosition: 'top',
      panelClass: 'cx-toast-warn',
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
      // problem+json as the API actually sends it; narrowing here keeps every caller honest.
      const body = err.error as { detail?: unknown; title?: unknown } | null | undefined;
      if (body && typeof body === 'object') {
        if (typeof body.detail === 'string' && body.detail) return body.detail;
        if (typeof body.title === 'string' && body.title) return body.title;
      }
      return this.transloco.translate('err_server_error');
    }
    return String(err);
  }
}
