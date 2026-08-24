import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';
import { buildReceiptSettingsRequest } from './receipt-settings-state';

@Component({
  selector: 'app-receipt-settings',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    TranslocoModule,
    PageHeader,
  ],
  templateUrl: './receipt-settings.html',
  styleUrl: './receipt-settings.scss',
})
export class ReceiptSettings implements OnInit {
  private readonly api = inject(SettingsApi);
  private readonly notify = inject(NotifyService);

  readonly canManage = inject(AuthService).hasPermission('settings.receipt');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly previewBusy = signal(false);
  readonly previewText = signal('');
  private previewTimer: number | undefined;

  headerText = '';
  footerText = '';
  paperWidth = 0;
  paperFormat = 'Thermal';
  language = 'uz-latn';
  readonly paperFormats = ['Thermal', 'A5', 'A4'];
  readonly paperWidths = [0, 32, 40, 42, 48, 64];
  readonly languages = [
    { code: 'uz-latn', name: "O'zbek (lotin)" },
    { code: 'uz-cyrl', name: 'Ўзбек (кирилл)' },
    { code: 'ru', name: 'Русский' },
    { code: 'en', name: 'English' },
  ];

  async ngOnInit(): Promise<void> {
    try {
      const s = await lastValueFrom(this.api.receipt());
      this.headerText = s.headerText ?? '';
      this.footerText = s.footerText ?? '';
      this.paperWidth = this.paperWidths.includes(s.paperWidth) ? s.paperWidth : 0;
      this.paperFormat = s.paperFormat || 'Thermal';
      this.language = s.language ?? 'uz-latn';
      await this.refreshPreview();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  async save(message: string): Promise<void> {
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.updateReceipt(buildReceiptSettingsRequest(this)),
      );
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  schedulePreview(): void {
    if (this.previewTimer !== undefined) window.clearTimeout(this.previewTimer);
    this.previewTimer = window.setTimeout(() => {
      void this.refreshPreview();
    }, 200);
  }

  private async refreshPreview(): Promise<void> {
    this.previewBusy.set(true);
    try {
      const result = await lastValueFrom(
        this.api.previewReceipt(buildReceiptSettingsRequest(this)),
      );
      this.previewText.set(result.text);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.previewBusy.set(false);
    }
  }
}
