import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CustomersApi } from '../../core/api.service';
import type { ConsolidatedAct, StatementEntry } from '../../core/api.service';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';

interface ActRow {
  kind: string;
  id: number;
  number: string;
  summary: string;
  occurredAt: string;
  amount: number;
  picked: boolean;
}

const KIND: Record<string, string> = {
  Sale: 'sale',
  CustomerReturn: 'return',
  CustomerPayment: 'payment',
  CustomerRefund: 'refund',
};

/// Desktopdagi bilan bir xil ikki qadamli tuzilma: chapda hujjatlar, o'ngda natija.
/// Uchala yakun doim ko'rinadi, natija paneli esa tuzilmagunicha tushuntirish ko'rsatadi —
/// shunda oyna hech qachon yarim bo'sh holatda ochilmaydi.
@Component({
  selector: 'app-consolidated-act-dialog',
  imports: [
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    TranslocoModule,
    CxDatePipe,
    CxMoneyPipe,
  ],
  templateUrl: './consolidated-act.dialog.html',
  styleUrl: './consolidated-act.dialog.scss',
})
export class ConsolidatedActDialog {
  private readonly api = inject(CustomersApi);
  private readonly notify = inject(NotifyService);
  private readonly data = inject<{ customerId: number; customerName: string; timeline: StatementEntry[] }>(
    MAT_DIALOG_DATA,
  );

  readonly customerName = this.data.customerName;
  readonly busy = signal(false);
  readonly act = signal<ConsolidatedAct | null>(null);
  readonly rows = signal<ActRow[]>(
    this.data.timeline
      .filter((e) => KIND[e.type] && e.documentId)
      .map((e) => ({
        kind: KIND[e.type],
        id: e.documentId!,
        number: e.documentNumber,
        summary: e.summary,
        occurredAt: e.occurredAt,
        // Balansga sof ta'sir: qarz qo'shsa musbat, yopsa manfiy. To'liq to'langan savdo
        // balansga tegmaydi, shuning uchun nol chiziqcha bo'lib chiqadi.
        amount: e.debit - e.credit,
        picked: false,
      })),
  );

  readonly selected = computed(() => this.rows().filter((r) => r.picked));
  readonly allPicked = computed(() => this.rows().length > 0 && this.selected().length === this.rows().length);
  readonly selectedAmount = computed(() => this.selected().reduce((sum, r) => sum + r.amount, 0));

  toggle(row: ActRow): void {
    this.rows.set(this.rows().map((r) => (r === row ? { ...r, picked: !r.picked } : r)));
  }

  toggleAll(): void {
    const select = !this.allPicked();
    this.rows.set(this.rows().map((r) => ({ ...r, picked: select })));
  }

  async generate(): Promise<void> {
    const picked = this.selected();
    if (!picked.length) return;
    this.busy.set(true);
    try {
      this.act.set(
        await lastValueFrom(
          this.api.consolidatedAct(
            this.data.customerId,
            picked.map((r) => ({ kind: r.kind, id: r.id })),
          ),
        ),
      );
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
