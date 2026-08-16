import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { StorageApi } from '../../core/api/catalog.api';
import { Business, BusinessApi, FeaturesApi } from '../../core/api/misc.api';
import { SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-business',
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
  templateUrl: './business.html',
  styleUrl: './business.scss',
})
export class BusinessSettings implements OnInit {
  private readonly api = inject(BusinessApi);
  private readonly settings = inject(SettingsApi);
  private readonly storage = inject(StorageApi);
  private readonly features = inject(FeaturesApi);
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly uploading = signal(false);
  readonly pricingMulticurrency = signal(false);
  readonly salesMulticurrency = signal(false);
  readonly logoPreview = signal<string | null>(null);
  readonly canManage = this.auth.hasPermission('business.edit');
  readonly canFeatures = this.auth.hasPermission('features.edit');
  readonly canSecurity = this.auth.hasPermission('settings.security');


  name = '';
  legalName = '';
  currency = '';
  phone = '';
  telegram = '';
  website = '';
  address = '';
  private logoImageKey: string | null = null;

  qrEnabled = false;
  qrRefreshSeconds = 120;
  keyEnabled = true;
  private loginLoaded = false;

  async ngOnInit(): Promise<void> {
    if (this.canSecurity) {
      try {
        const login = await lastValueFrom(this.settings.loginMethods());
        this.qrEnabled = login.qrEnabled;
        this.qrRefreshSeconds = login.qrRefreshSeconds;
        this.keyEnabled = login.keyEnabled;
        this.loginLoaded = true;
      } catch {}
    }
    try {
      this.apply(await lastValueFrom(this.api.get()));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  async onLogoFile(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.uploading.set(true);
    try {
      const result = await lastValueFrom(this.storage.upload(file));
      this.logoImageKey = result.key;
      this.logoPreview.set(URL.createObjectURL(file));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.uploading.set(false);
    }
  }

  async save(message: string): Promise<void> {
    if (!this.name.trim()) return;
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.update({
          name: this.name.trim(),
          legalName: this.legalName.trim() || null,
          currency: this.currency,
          phone: this.phone.trim() || null,
          telegram: this.telegram.trim() || null,
          website: this.website.trim() || null,
          address: this.address.trim() || null,
          logoImageKey: this.logoImageKey,
        }),
      );
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async saveLoginMethods(message: string): Promise<void> {
    if (!this.canSecurity || !this.loginLoaded) return;
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.settings.updateLoginMethods({
          qrEnabled: this.qrEnabled,
          qrRefreshSeconds: Math.min(600, Math.max(30, Math.round(this.qrRefreshSeconds) || 120)),
          keyEnabled: this.keyEnabled,
        }),
      );
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async toggleMulticurrency(feature: 'multicurrency_pricing' | 'multicurrency_sales', value: boolean, message: string): Promise<void> {
    const target = feature === 'multicurrency_pricing' ? this.pricingMulticurrency : this.salesMulticurrency;
    target.set(value);
    this.busy.set(true);
    try {
      await lastValueFrom(this.features.set(feature, value));
      this.notify.success(message);
    } catch (e) {
      target.set(!value);
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  private apply(b: Business): void {
    this.name = b.name;
    this.legalName = b.legalName ?? '';
    this.currency = b.currency;
    this.phone = b.phone ?? '';
    this.telegram = b.telegram ?? '';
    this.website = b.website ?? '';
    this.address = b.address ?? '';
    this.logoImageKey = b.logoImageKey;
    if (b.logoImageKey) this.logoPreview.set(`/api/storage/content?key=${encodeURIComponent(b.logoImageKey)}`);
    this.pricingMulticurrency.set(b.pricingMulticurrency);
    this.salesMulticurrency.set(b.salesMulticurrency);
  }
}
