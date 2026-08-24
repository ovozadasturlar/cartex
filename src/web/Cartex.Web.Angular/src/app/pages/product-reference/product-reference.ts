import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { ProductReferenceSettings, SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-product-reference-settings',
  imports: [
    FormsModule,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    TranslocoModule,
    PageHeader,
  ],
  templateUrl: './product-reference.html',
  styleUrl: './product-reference.scss',
})
export class ProductReferenceSettingsPage implements OnInit {
  private readonly api = inject(SettingsApi);
  private readonly notify = inject(NotifyService);
  private readonly auth = inject(AuthService);

  readonly canManage = this.auth.hasPermission('settings.salesPolicy');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly schedules = ['Manual', 'Daily'];

  model: ProductReferenceSettings = {
    isEnabled: false,
    sourceType: 'GoogleSheets',
    spreadsheetId: '',
    sheetName: '',
    barcodeColumn: 'barcode',
    nameColumn: 'name',
    unitColumn: 'unit',
    categoryColumn: 'category',
    manufacturerColumn: 'manufacturer',
    packQtyColumn: 'pack_qty',
    priceColumn: 'price',
    autoFillPrice: false,
    syncSchedule: 'Manual',
    lastSyncedAt: null,
    lastReadCount: 0,
    lastUpdatedCount: 0,
    lastErrorCount: 0,
    rowCount: 0,
    lastError: null,
  };

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    try {
      this.model = await lastValueFrom(this.api.productReference());
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  async save(message: string): Promise<void> {
    if (!this.canManage) return;
    this.busy.set(true);
    try {
      await lastValueFrom(this.api.updateProductReference(this.model));
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async sync(message: string): Promise<void> {
    if (!this.canManage) return;
    this.busy.set(true);
    try {
      const result = await lastValueFrom(this.api.syncProductReference());
      this.model = {
        ...this.model,
        lastSyncedAt: result.syncedAt,
        lastReadCount: result.read,
        lastUpdatedCount: result.updated,
        lastErrorCount: result.errors,
        rowCount: result.total,
        lastError: null,
      };
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }
}
