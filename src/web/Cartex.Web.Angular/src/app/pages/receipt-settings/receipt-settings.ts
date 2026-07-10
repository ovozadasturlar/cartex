import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-receipt-settings',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    TranslocoModule,
    PageHeader,
  ],
  templateUrl: './receipt-settings.html',
  styleUrl: './receipt-settings.scss',
})
export class ReceiptSettings implements OnInit {
  private readonly api = inject(SettingsApi);
  private readonly notify = inject(NotifyService);

  readonly canManage = inject(AuthService).hasPermission('business.manage');
  readonly loading = signal(true);
  readonly busy = signal(false);

  headerText = '';
  footerText = '';
  paperWidth = 32;

  async ngOnInit(): Promise<void> {
    try {
      const s = await lastValueFrom(this.api.receipt());
      this.headerText = s.headerText ?? '';
      this.footerText = s.footerText ?? '';
      this.paperWidth = s.paperWidth;
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
        this.api.updateReceipt({
          headerText: this.headerText.trim() || null,
          footerText: this.footerText.trim() || null,
          paperWidth: this.paperWidth,
        }),
      );
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
