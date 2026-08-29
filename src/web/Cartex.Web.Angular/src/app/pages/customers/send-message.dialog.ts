import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CustomersApi } from '../../core/api.service';
import { Customer } from '../../core/models';
import { NotifyService } from '../../core/notify.service';

/// Kanal mijozda bori bilan cheklanadi: Telegram ulanmagan bo'lsa yoki telefon yozilmagan
/// bo'lsa, o'sha yo'l umuman taklif qilinmaydi.
@Component({
  selector: 'app-send-message-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    TranslocoModule,
  ],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ t('customer_message') }}</h2>
      <mat-dialog-content>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
          <mat-label>{{ t('channel') }}</mat-label>
          <mat-select [(ngModel)]="channel" cdkFocusInitial>
            @for (c of channels(); track c) {
              <mat-option [value]="c">{{ t('channel_' + c) }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="full">
          <mat-label>{{ t('message_text') }}</mat-label>
          <textarea matInput rows="4" [(ngModel)]="text"></textarea>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button matButton mat-dialog-close>{{ t('cancel') }}</button>
        <button matButton="filled" [disabled]="!canSend() || busy()" (click)="send(t('success'))">
          {{ t('send') }}
        </button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .full { width: 100%; margin-bottom: 12px; }
  `,
})
export class SendMessageDialog {
  private readonly api = inject(CustomersApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject(MatDialogRef<SendMessageDialog>);
  private readonly customer = inject<Customer>(MAT_DIALOG_DATA);

  readonly busy = signal(false);
  // SMS-34: `auto` da serverning o'zi yoqilgan birinchi mos kanalni tanlaydi - klient
  // qaysi kanal sozlanganini bilishi shart emas, shuning uchun u birinchi turadi.
  readonly channels = signal<string[]>(
    [
      'auto',
      this.customer.hasTelegram ? 'telegram' : null,
      this.customer.phone ? 'sms' : null,
      this.customer.email ? 'email' : null,
    ].filter((c): c is string => c !== null),
  );

  channel = this.channels()[0] ?? '';
  text = '';

  readonly canSend = computed(() => this.channels().length > 1);

  async send(message: string): Promise<void> {
    if (!this.channel || !this.text.trim()) return;
    this.busy.set(true);
    try {
      await lastValueFrom(this.api.sendMessage(this.customer.id, this.channel, this.text.trim()));
      this.notify.success(message);
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
