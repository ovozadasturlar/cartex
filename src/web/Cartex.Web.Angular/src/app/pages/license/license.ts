import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { FeaturesApi } from '../../core/api/misc.api';
import { LicenseApi, LicenseOptions, LicenseStatus } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-license',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    TranslocoModule,
    CxDatePipe,
    PageHeader,
  ],
  templateUrl: './license.html',
  styleUrl: './license.scss',
})
export class License implements OnInit {
  private readonly api = inject(LicenseApi);
  private readonly features = inject(FeaturesApi);
  private readonly notify = inject(NotifyService);

  readonly canManage = inject(AuthService).hasPermission('settings.manage');
  readonly canFeatures = inject(AuthService).hasPermission('features.manage');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly status = signal<LicenseStatus | null>(null);
  readonly options = signal<LicenseOptions | null>(null);
  readonly enabledNames = computed(() => {
    const s = this.status();
    const o = this.options();
    if (!s || !o) return [];
    return s.enabledFeatures.map((c) => o.features.find((f) => f.code === c)?.name ?? c);
  });

  tariff = '';
  expires = '';

  async ngOnInit(): Promise<void> {
    try {
      const [options, status] = await Promise.all([
        lastValueFrom(this.api.options()),
        lastValueFrom(this.api.get()),
      ]);
      this.options.set(options);
      this.status.set(status);
      this.tariff = options.tariffs.includes(status.tariff) ? status.tariff : (options.tariffs[0] ?? '');
      this.expires = status.expiresAt?.slice(0, 10) ?? '';
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  async save(message: string): Promise<void> {
    const options = this.options();
    if (!options || !this.tariff) return;
    this.busy.set(true);
    try {
      const codes = options.features.filter((f) => f.includedTariffs.includes(this.tariff)).map((f) => f.code);
      await lastValueFrom(
        this.api.update({
          tariff: this.tariff,
          expiresAt: this.expires ? `${this.expires}T00:00:00Z` : null,
          enabledFeatures: JSON.stringify(codes),
        }),
      );
      if (this.canFeatures) {
        for (const f of options.features) {
          await lastValueFrom(this.features.set(f.code, codes.includes(f.code)));
        }
      }
      this.status.set(await lastValueFrom(this.api.get()));
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
