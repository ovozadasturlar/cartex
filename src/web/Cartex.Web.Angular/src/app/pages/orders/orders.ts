import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { Cart, CartListItem, CartLoadItem, FeaturesApi, OrderingApi } from '../../core/api/misc.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe, CxMoneyPipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';
import { ConfirmDialog } from '../loyalty/confirm-dialog';

const chipClasses: Record<string, string> = {
  Ready: 'ok',
  CheckedOut: 'ok',
  Cancelled: 'bad',
  Open: 'warn',
};

@Component({
  selector: 'app-orders',
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxDatePipe,
    EmptyState,
    PageHeader,
  ],
  templateUrl: './orders.html',
  styleUrl: './orders.scss',
})
export class Orders implements OnInit, OnDestroy {
  private readonly api = inject(OrderingApi);
  private readonly features = inject(FeaturesApi);
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private refreshTimer?: ReturnType<typeof setTimeout>;
  private readonly refresh = (): void => {
    if (document.visibilityState !== 'visible') return;
    clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(() => {
      if (this.enabled() && !this.loading() && !this.busy()) void this.load();
    }, 400);
  };

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly enabled = signal(false);
  readonly carts = signal<CartListItem[]>([]);
  readonly status = signal('Open');
  readonly statuses = ['', 'Open', 'Confirmed', 'Ready', 'CheckedOut', 'Cancelled'];
  readonly cols = ['code', 'customer', 'warehouse', 'items', 'date', 'status'];
  readonly canManage = this.auth.hasPermission('sales.pick|sales.create|sales.checkout');
  readonly canViewLoad = this.auth.hasPermission('sales.view');

  async ngOnInit(): Promise<void> {
    document.addEventListener('visibilitychange', this.refresh);
    window.addEventListener('focus', this.refresh);
    try {
      const enabled = await lastValueFrom(this.features.enabled());
      this.enabled.set(enabled.includes('ordering'));
      if (this.enabled()) await this.load();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  ngOnDestroy(): void {
    clearTimeout(this.refreshTimer);
    document.removeEventListener('visibilitychange', this.refresh);
    window.removeEventListener('focus', this.refresh);
  }

  setStatus(value: string): void {
    this.status.set(value);
    void this.load();
  }

  statusKey(status: string): string {
    return 'cart_status_' + status.toLowerCase();
  }

  chipClass(status: string): string {
    return chipClasses[status] ?? '';
  }

  open(row: CartListItem): void {
    if (!this.canManage) return;
    this.dialog
      .open(OrderDialog, { data: row.aggregateCode, width: '640px', maxWidth: '94vw', autoFocus: false })
      .afterClosed()
      .subscribe((changed) => {
        if (changed) void this.load();
      });
  }

  openLoad(): void {
    if (!this.canViewLoad) return;
    this.dialog.open(LoadDialog, { data: this.status() || undefined, width: '520px', maxWidth: '94vw', autoFocus: false });
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.carts.set(await lastValueFrom(this.api.list(this.status() || undefined, 'Order')));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-order-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    CxMoneyPipe,
  ],
  styleUrl: './orders.scss',
  template: `
    <ng-container *transloco="let t">
      <div class="dlg-head">
        <div>
          <h2>{{ t('cart') }} · {{ code.slice(0, 8) }}</h2>
          @if (cart(); as c) {
            <p>{{ c.customerName || t('all_customers') }} · <span class="cx-chip">{{ t('cart_status_' + c.status.toLowerCase()) }}</span></p>
          }
        </div>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <mat-dialog-content>
        @if (busy()) {
          <mat-progress-bar mode="indeterminate" />
        }
        @if (cart(); as c) {
          <table mat-table [dataSource]="c.items" class="items">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>{{ t('product_name') }}</th>
              <td mat-cell *matCellDef="let i">{{ i.productName }}</td>
            </ng-container>
            <ng-container matColumnDef="qty">
              <th mat-header-cell *matHeaderCellDef class="num">{{ t('quantity') }}</th>
              <td mat-cell *matCellDef="let i" class="num">{{ i.quantity }}</td>
            </ng-container>
            <ng-container matColumnDef="price">
              <th mat-header-cell *matHeaderCellDef class="num">{{ t('price') }}</th>
              <td mat-cell *matCellDef="let i" class="num cx-money">{{ i.unitPrice | cxMoney }}</td>
            </ng-container>
            <ng-container matColumnDef="total">
              <th mat-header-cell *matHeaderCellDef class="num">{{ t('total') }}</th>
              <td mat-cell *matCellDef="let i" class="num cx-money">{{ i.lineTotal | cxMoney }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="cols"></tr>
            <tr mat-row *matRowDef="let i; columns: cols"></tr>
          </table>
          <div class="grand">
            <span>{{ t('total') }}</span>
            <span class="cx-money">{{ c.total | cxMoney }}</span>
          </div>
          @if (paying()) {
            <div class="pay-form">
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ t('paid_cash') }}</mat-label>
                <input matInput type="number" min="0" [(ngModel)]="paidCash" />
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ t('paid_card') }}</mat-label>
                <input matInput type="number" min="0" [(ngModel)]="paidCard" />
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ t('bonus') }}</mat-label>
                <input matInput type="number" min="0" [(ngModel)]="paidBonus" />
              </mat-form-field>
            </div>
          }
        }
      </mat-dialog-content>
      @if (cart(); as c) {
        <mat-dialog-actions align="end">
          @if (paying()) {
            <button matButton (click)="paying.set(false)">{{ t('cancel') }}</button>
            <button matButton="filled" [disabled]="busy()" (click)="checkout(t('checkout_done'))">
              <mat-icon>point_of_sale</mat-icon>{{ t('complete_sale') }}
            </button>
          } @else {
            @if (canPick && (c.status === 'Open' || c.status === 'Confirmed' || c.status === 'Ready')) {
              <button matButton class="danger" [disabled]="busy()" (click)="cancelOrder(t('success'))">{{ t('cancel') }}</button>
            }
            @if (canCheckout && c.status === 'Open') {
              <button matButton="filled" [disabled]="busy()" (click)="setStatus('Confirmed', t('success'))">{{ t('confirm') }}</button>
            }
            @if (canCheckout && c.status === 'Confirmed') {
              <button matButton="filled" [disabled]="busy()" (click)="setStatus('Ready', t('success'))">{{ t('order_ready') }}</button>
            }
            @if (canCheckout && (c.status === 'Confirmed' || c.status === 'Ready')) {
              <button matButton="filled" [disabled]="busy()" (click)="startCheckout()">
                <mat-icon>point_of_sale</mat-icon>{{ t('checkout') }}
              </button>
            }
          }
        </mat-dialog-actions>
      }
    </ng-container>
  `,
})
export class OrderDialog implements OnInit {
  private readonly api = inject(OrderingApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly ref = inject(MatDialogRef<OrderDialog>);
  private readonly auth = inject(AuthService);

  readonly code = inject<string>(MAT_DIALOG_DATA);
  readonly cart = signal<Cart | null>(null);
  readonly busy = signal(true);
  readonly paying = signal(false);
  readonly cols = ['name', 'qty', 'price', 'total'];
  readonly canPick = this.auth.hasPermission('sales.pick|sales.create|sales.checkout');
  readonly canCheckout = this.auth.hasPermission('sales.checkout');

  paidCash = 0;
  paidCard = 0;
  paidBonus = 0;

  async ngOnInit(): Promise<void> {
    try {
      this.cart.set(await lastValueFrom(this.api.byCode(this.code)));
    } catch (e) {
      this.notify.error(e);
      this.ref.close();
    } finally {
      this.busy.set(false);
    }
  }

  startCheckout(): void {
    if (!this.canCheckout) return;
    this.paidCash = this.cart()?.total ?? 0;
    this.paidCard = 0;
    this.paidBonus = 0;
    this.paying.set(true);
  }

  async setStatus(status: string, message: string): Promise<void> {
    if (!this.canPick || (status !== 'Cancelled' && !this.canCheckout)) return;
    this.busy.set(true);
    try {
      await lastValueFrom(this.api.updateStatus(this.code, status));
      this.notify.success(message);
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
      this.busy.set(false);
    }
  }

  cancelOrder(message: string): void {
    if (!this.canPick) return;
    this.dialog
      .open(ConfirmDialog, { data: 'order_cancel_confirm', width: '380px' })
      .afterClosed()
      .subscribe((ok) => {
        if (ok) void this.setStatus('Cancelled', message);
      });
  }

  async checkout(message: string): Promise<void> {
    if (!this.canCheckout) return;
    this.busy.set(true);
    try {
      const sale = await lastValueFrom(
        this.api.checkout(this.code, this.paidCash || 0, this.paidCard || 0, this.paidBonus || 0),
      );
      this.notify.success(`${message} #${sale.saleId}`);
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-load-dialog',
  imports: [MatButtonModule, MatDialogModule, MatIconModule, MatProgressBarModule, MatTableModule, TranslocoModule, EmptyState],
  styleUrl: './orders.scss',
  template: `
    <ng-container *transloco="let t">
      <div class="dlg-head">
        <h2>{{ t('load_list') }}</h2>
        <button matIconButton mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <mat-dialog-content>
        @if (busy()) {
          <mat-progress-bar mode="indeterminate" />
        } @else if (items().length) {
          <table mat-table [dataSource]="items()" class="items">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>{{ t('product_name') }}</th>
              <td mat-cell *matCellDef="let i">{{ i.productName }}</td>
            </ng-container>
            <ng-container matColumnDef="unit">
              <th mat-header-cell *matHeaderCellDef>{{ t('unit') }}</th>
              <td mat-cell *matCellDef="let i">{{ i.unitName }}</td>
            </ng-container>
            <ng-container matColumnDef="qty">
              <th mat-header-cell *matHeaderCellDef class="num">{{ t('total_quantity') }}</th>
              <td mat-cell *matCellDef="let i" class="num cx-money">{{ i.totalQuantity }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="cols"></tr>
            <tr mat-row *matRowDef="let i; columns: cols"></tr>
          </table>
        } @else {
          <cx-empty-state icon="local_shipping" [message]="t('no_items')" />
        }
      </mat-dialog-content>
    </ng-container>
  `,
})
export class LoadDialog implements OnInit {
  private readonly api = inject(OrderingApi);
  private readonly notify = inject(NotifyService);

  readonly status = inject<string | undefined>(MAT_DIALOG_DATA);
  readonly items = signal<CartLoadItem[]>([]);
  readonly busy = signal(true);
  readonly cols = ['name', 'unit', 'qty'];

  async ngOnInit(): Promise<void> {
    try {
      this.items.set(await lastValueFrom(this.api.load(this.status)));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
