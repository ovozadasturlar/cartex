import { Component, OnInit, inject, signal } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { FeaturesApi } from '../../core/api/misc.api';
import { Feature, LicenseApi, SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-features',
  imports: [MatProgressBarModule, MatSlideToggleModule, MatTableModule, TranslocoModule, PageHeader],
  templateUrl: './features.html',
  styleUrl: './features.scss',
})
export class Features implements OnInit {
  private readonly api = inject(SettingsApi);
  private readonly licenseApi = inject(LicenseApi);
  private readonly featuresApi = inject(FeaturesApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);

  readonly canManage = inject(AuthService).hasPermission('features.manage');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly items = signal<Feature[]>([]);
  readonly tariffFeatures = signal<string[] | null>(null);
  readonly columns = ['name', 'code', 'description', 'tariff', 'enabled'];

  async ngOnInit(): Promise<void> {
    try {
      this.items.set(await lastValueFrom(this.api.allFeatures()));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
    try {
      this.tariffFeatures.set((await lastValueFrom(this.licenseApi.get())).enabledFeatures);
    } catch {
      this.tariffFeatures.set(null);
    }
  }

  desc(code: string): string {
    const key = `feature_effect_${code}`;
    const value = this.transloco.translate(key);
    return value === key ? '' : value;
  }

  inTariff(code: string): boolean {
    const codes = this.tariffFeatures();
    return codes === null || codes.includes(code);
  }

  async toggle(feature: Feature, value: boolean, message: string): Promise<void> {
    this.busy.set(true);
    try {
      await lastValueFrom(this.featuresApi.set(feature.code, value));
      this.items.update((all) => all.map((f) => (f.code === feature.code ? { ...f, isEnabled: value } : f)));
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
