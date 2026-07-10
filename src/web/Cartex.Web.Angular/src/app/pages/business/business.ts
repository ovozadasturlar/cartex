import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { Business, BusinessApi, FeaturesApi } from '../../core/api/misc.api';
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
    MatSlideToggleModule,
    TranslocoModule,
    PageHeader,
  ],
  templateUrl: './business.html',
  styleUrl: './business.scss',
})
export class BusinessSettings implements OnInit {
  private readonly api = inject(BusinessApi);
  private readonly features = inject(FeaturesApi);
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotifyService);

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly multicurrency = signal(false);
  readonly canManage = this.auth.hasPermission('business.manage');
  readonly canFeatures = this.auth.hasPermission('features.manage');

  name = '';
  legalName = '';
  currency = '';
  phone = '';
  address = '';
  private logoImageKey: string | null = null;

  async ngOnInit(): Promise<void> {
    try {
      this.apply(await lastValueFrom(this.api.get()));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
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

  async toggleMulticurrency(value: boolean, message: string): Promise<void> {
    this.multicurrency.set(value);
    this.busy.set(true);
    try {
      await lastValueFrom(this.features.set('multicurrency', value));
      this.notify.success(message);
    } catch (e) {
      this.multicurrency.set(!value);
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
    this.address = b.address ?? '';
    this.logoImageKey = b.logoImageKey;
    this.multicurrency.set(b.multicurrency);
  }
}
