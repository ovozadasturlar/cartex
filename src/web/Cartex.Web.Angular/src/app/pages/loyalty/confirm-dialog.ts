import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { TranslocoModule } from '@jsverse/transloco';

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatButtonModule, MatDialogModule, TranslocoModule],
  template: `
    <ng-container *transloco="let t">
      <mat-dialog-content>
        <p class="msg">{{ t(messageKey) }}</p>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [mat-dialog-close]="true">{{ t('confirm') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .msg { margin: 8px 0 0; font-size: 14px; color: var(--cx-text-1); }
  `,
})
export class ConfirmDialog {
  readonly messageKey = inject<string>(MAT_DIALOG_DATA);
}
