import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { FeaturesApi } from '../../core/api/misc.api';
import { LicenseApi, LicenseOptions, LicenseStatus } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { FeaturesService } from '../../core/features.service';
import { isoDay } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

interface FeatureRow {
  code: string;
  name: string;
  impact: string;
  isEnabled: boolean;
}

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
    MatSlideToggleModule,
    TranslocoModule,
    PageHeader,
  ],
  templateUrl: './license.html',
  styleUrl: './license.scss',
})
export class License implements OnInit {
  private readonly api = inject(LicenseApi);
  private readonly featuresApi = inject(FeaturesApi);
  private readonly features = inject(FeaturesService);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);

  readonly canFeatures = inject(AuthService).hasPermission('features.edit');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly status = signal<LicenseStatus | null>(null);
  readonly rows = signal<FeatureRow[]>([]);
  readonly options = signal<LicenseOptions | null>(null);

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
      this.expires = status.expiresAt ? isoDay(new Date(status.expiresAt)) : '';
      const enabled = status.enabledFeatures.length
        ? status.enabledFeatures
        : options.features.filter((f) => f.includedTariffs.includes(status.tariff)).map((f) => f.code);
      this.rows.set(
        options.features.map((f) => ({
          code: f.code,
          name: f.name,
          impact: f.permissions.length ? f.permissions.join(', ') : this.desc(f.code),
          isEnabled: enabled.includes(f.code),
        })),
      );
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  onTariffChange(value: string): void {
    if (!this.canFeatures) return;
    const options = this.options();
    if (!options) return;
    this.rows.update((all) =>
      all.map((r) => ({
        ...r,
        isEnabled: options.features.find((f) => f.code === r.code)?.includedTariffs.includes(value) ?? false,
      })),
    );
  }

  toggle(code: string, value: boolean): void {
    if (!this.canFeatures) return;
    this.rows.update((all) => all.map((r) => (r.code === code ? { ...r, isEnabled: value } : r)));
  }

  async save(message: string): Promise<void> {
    if (!this.canFeatures || !this.tariff) return;
    this.busy.set(true);
    try {
      const codes = this.rows().filter((r) => r.isEnabled).map((r) => r.code);
      await lastValueFrom(
        this.api.update({
          tariff: this.tariff,
          expiresAt: this.expires ? new Date(this.expires + 'T00:00:00').toISOString() : null,
          enabledFeatures: JSON.stringify(codes),
        }),
      );
      for (const r of this.rows()) {
        await lastValueFrom(this.featuresApi.set(r.code, r.isEnabled));
      }
      // RUXSAT-04a: tarif o'zgarishi imkoniyatlar ro'yxatini eskirtiradi - menyu va qorovullar
      // shu yerdan darhol yangilanishi kerak.
      this.features.reset();
      this.status.set(await lastValueFrom(this.api.get()));
      await this.features.ensureLoaded();
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  private desc(code: string): string {
    const key = `feature_effect_${code}`;
    const value = this.transloco.translate(key);
    return value === key ? '' : value;
  }
}
