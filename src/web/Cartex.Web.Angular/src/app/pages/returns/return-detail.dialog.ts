import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { CustomerReturnDocument } from '../../core/api/returns.api';
import { CxDatePipe, CxEnumPipe, CxMoneyPipe } from '../../core/format';
import { RemotePrintService } from '../../core/remote-print.service';

@Component({
  selector: 'app-return-detail-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    CxEnumPipe,
    CxMoneyPipe,
  ],
  template: `
    <ng-container *transloco="let t">
      <h2 mat-dialog-title>{{ doc.documentNumber }}</h2>
      <mat-dialog-content>
        <div class="head">
          <span>{{ doc.createdAt | cxDate }}</span>
          <span>{{ doc.customerName || t('walk_in') }}</span>
          <span>{{ doc.warehouseName }}</span>
          <span>{{ doc.userName }}</span>
        </div>

        <table mat-table [dataSource]="doc.lines" class="cx-dense">
          <ng-container matColumnDef="product">
            <th mat-header-cell *matHeaderCellDef>{{ t('product') }}</th>
            <td mat-cell *matCellDef="let r">{{ r.productName }}</td>
          </ng-container>
          <ng-container matColumnDef="quantity">
            <th mat-header-cell *matHeaderCellDef>{{ t('qty') }}</th>
            <td mat-cell *matCellDef="let r">{{ r.quantity }} {{ r.unitName }}</td>
          </ng-container>
          <ng-container matColumnDef="price">
            <th mat-header-cell *matHeaderCellDef>{{ t('price') }}</th>
            <td mat-cell *matCellDef="let r" class="cx-money">{{ r.unitPrice | cxMoney }}</td>
          </ng-container>
          <ng-container matColumnDef="total">
            <th mat-header-cell *matHeaderCellDef>{{ t('total') }}</th>
            <td mat-cell *matCellDef="let r" class="cx-money">{{ r.lineAmount | cxMoney }}</td>
          </ng-container>
          <ng-container matColumnDef="reason">
            <th mat-header-cell *matHeaderCellDef>{{ t('reason') }}</th>
            <td mat-cell *matCellDef="let r">{{ r.reason || '—' }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="cols"></tr>
          <tr mat-row *matRowDef="let row; columns: cols"></tr>
        </table>

        @if (doc.settlements.length) {
          <div class="settlements">
            @for (s of doc.settlements; track $index) {
              <span>{{ s.method | cxEnum: 'settle' }}: {{ s.amountBase | cxMoney }}</span>
            }
          </div>
        }

        <div class="totals">
          <span>{{ t('total') }}</span>
          <strong class="cx-money">{{ doc.refundAmount | cxMoney }}</strong>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        @if (canPrint) {
          <button matButton [disabled]="printing()" (click)="print()">
            <mat-icon>print</mat-icon>{{ t('print') }}
          </button>
        }
        <button matButton mat-dialog-close>{{ t('close') }}</button>
      </mat-dialog-actions>
    </ng-container>
  `,
  styles: `
    .head {
      display: flex;
      flex-wrap: wrap;
      gap: 14px;
      margin-bottom: 12px;
      color: var(--cx-text-2);
      font-size: 13px;
    }
    .settlements {
      display: flex;
      gap: 14px;
      margin-top: 10px;
      font-size: 13px;
      color: var(--cx-text-2);
    }
    .totals {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      margin-top: 14px;
      padding-top: 12px;
      border-top: 1px solid var(--cx-border);
    }
    .totals strong { font-size: 18px; }
  `,
})
export class ReturnDetailDialog {
  private readonly remotePrint = inject(RemotePrintService);
  readonly doc = inject<CustomerReturnDocument>(MAT_DIALOG_DATA);
  readonly cols = ['product', 'quantity', 'price', 'total', 'reason'];
  readonly canPrint = this.remotePrint.can('printing.documents.print');
  readonly printing = signal(false);

  // Qaytarish cheki desktopdagi kabi tarmoq printeriga yuboriladi.
  async print(): Promise<void> {
    this.printing.set(true);
    try {
      await this.remotePrint.send({
        kind: 'Receipt',
        permission: 'printing.documents.print',
        sourceType: 'customer_return',
        sourceId: String(this.doc.id),
        payload: { returnId: this.doc.id },
      });
    } finally {
      this.printing.set(false);
    }
  }
}
