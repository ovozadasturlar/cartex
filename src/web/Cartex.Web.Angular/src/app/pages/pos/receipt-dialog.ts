import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import QRCode from 'qrcode';
import { lastValueFrom } from 'rxjs';
import { CustomersApi, SalesApi } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe, newUuid } from '../../core/format';
import { Customer, Receipt } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { ConfirmDialog } from '../loyalty/confirm-dialog';
import { CustomerPickerDialog } from './pos-dialogs';

export interface ReceiptDialogData {
  receipt: Receipt;
  saleId?: number | null;
  /// Kassada savdo yakunlanganda "Yopish" o'rniga "Yangi sotuv" ko'rsatiladi — desktopdagi kabi.
  posCheckout?: boolean;
}

@Component({
  selector: 'app-pos-receipt-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
  ],
  templateUrl: './receipt-dialog.html',
  styleUrl: './receipt-dialog.scss',
})
export class PosReceiptDialog implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly ref = inject<MatDialogRef<PosReceiptDialog>>(MatDialogRef);
  private readonly router = inject(Router);
  private readonly salesApi = inject(SalesApi);
  private readonly customersApi = inject(CustomersApi);
  private readonly transloco = inject(TranslocoService);

  private readonly data = inject<ReceiptDialogData | Receipt>(MAT_DIALOG_DATA);

  // Eski chaqiruvlar chekni to'g'ridan-to'g'ri uzatadi, yangilari — obyekt bilan.
  readonly receipt: Receipt =
    'receipt' in this.data ? this.data.receipt : this.data;
  private readonly saleId = 'receipt' in this.data ? this.data.saleId ?? null : null;
  readonly isPosCheckout = 'receipt' in this.data ? !!this.data.posCheckout : false;

  readonly qr = signal<string | null>(null);

  /// Bekor qilingan savdoni na qaytarish, na tuzatish mumkin (QAYT-08).
  private readonly isOpen = (this.receipt.status ?? 'Completed') !== 'Voided';

  readonly canPrint =
    this.auth.hasPermission('printing.receipts.print') ||
    this.auth.hasPermission('printing.receipts.reprint');
  readonly canAttachCustomer =
    !this.receipt.customerName &&
    this.auth.hasPermission('sales.assignCustomer') &&
    this.saleId !== null;
  readonly canCorrect = this.isOpen && this.auth.hasPermission('sales.void') && this.saleId !== null;
  readonly canReturn = this.isOpen && this.auth.hasPermission('returns.create') && !this.isPosCheckout;
  readonly showReceiptSms = this.auth.hasPermission('customers.message') && this.saleId !== null;

  async ngOnInit(): Promise<void> {
    try {
      this.qr.set(
        await QRCode.toDataURL(`${location.origin}/r/${this.receipt.receiptToken}`, {
          margin: 0,
          width: 300,
        }),
      );
    } catch {
      this.qr.set(null);
    }
  }

  async attachCustomer(): Promise<void> {
    const picked: Customer | undefined = await lastValueFrom(
      this.dialog
        .open<CustomerPickerDialog, unknown, Customer>(CustomerPickerDialog, { autoFocus: 'input' })
        .afterClosed(),
    );
    if (!picked || this.saleId === null) return;
    try {
      await lastValueFrom(this.salesApi.assignCustomer(this.saleId, picked.id));
      this.receipt.customerName = picked.fullName;
      this.receipt.customerPhone = picked.phone;
      this.notify.success(this.transloco.translate('success'));
    } catch (e) {
      this.notify.error(e);
    }
  }

  /// Tuzatish savdoni bekor qilib, savatni qayta ochadi — shuning uchun tasdiq so'raladi.
  async correct(): Promise<void> {
    if (this.saleId === null) return;
    const ok = await lastValueFrom(
      this.dialog.open<ConfirmDialog, unknown, boolean>(ConfirmDialog, { data: 'correct_sale_confirm', width: '400px' }).afterClosed(),
    );
    if (!ok) return;
    try {
      await lastValueFrom(
        this.salesApi.voidSale(this.saleId, this.transloco.translate('correct_sale')),
      );
      this.ref.close('corrected');
      await this.router.navigate(['/pos']);
    } catch (e) {
      this.notify.error(e);
    }
  }

  returnSale(): void {
    this.ref.close('return');
    void this.router.navigate(['/returns']);
  }

  async sendReceiptSms(): Promise<void> {
    if (this.saleId === null || !this.receipt.customerPhone) return;
    try {
      const preview = await lastValueFrom(
        this.http.get<{ recipient: string; text: string; confirmationToken: string }>(
          `/api/sales/${this.saleId}/receipt-sms-preview`,
        ),
      );
      if (!window.confirm(this.transloco.translate('receipt_sms_confirm', preview))) return;
      await lastValueFrom(
        this.http.post<{ jobId: number; status: string }>(`/api/sales/${this.saleId}/receipt-sms`, {
          confirmationToken: preview.confirmationToken,
        }),
      );
      this.notify.success(this.transloco.translate('receipt_sms_queued'));
    } catch (error) {
      this.notify.error(error);
    }
  }

  async print(): Promise<void> {
    if (
      this.auth.hasPermission('printing.remote.use') &&
      this.auth.hasPermission('printing.receipts.print')
    ) {
      try {
        const context = await lastValueFrom(
          this.http.get<{ defaultBranchId: number; branches: { id: number }[] }>('/api/auth/context'),
        );
        const branchId = context.defaultBranchId || context.branches[0]?.id;
        if (branchId) {
          await lastValueFrom(
            this.http.post('/api/printing/jobs', {
              branchId,
              kind: 'Receipt',
              sourceType: 'receipt_token',
              sourceId: this.receipt.receiptToken,
              payload: { receiptToken: this.receipt.receiptToken },
              copies: 1,
              idempotencyKey: `receipt:${this.receipt.receiptToken}:web:${newUuid()}`,
            }),
          );
          return;
        }
      } catch (error) {
        this.notify.error(error);
      }
    }
    window.open('/r/' + this.receipt.receiptToken, '_blank');
  }
}
