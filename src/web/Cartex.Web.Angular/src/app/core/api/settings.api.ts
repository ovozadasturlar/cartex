import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface LicenseStatus {
  isActive: boolean;
  tariff: string;
  expiresAt: string | null;
  enabledFeatures: string[];
}

export interface LicenseFeature {
  code: string;
  name: string;
  includedTariffs: string[];
  permissions: string[];
}

export interface LicenseOptions {
  tariffs: string[];
  features: LicenseFeature[];
}

export interface Feature {
  code: string;
  name: string;
  isEnabled: boolean;
}

export interface TelegramSettings {
  enabled: boolean;
  chatId: string | null;
  hasBotToken: boolean;
}

export interface EmailSettings {
  enabled: boolean;
  host: string | null;
  port: number;
  useSsl: boolean;
  username: string | null;
  fromAddress: string | null;
  fromName: string | null;
  hasPassword: boolean;
}

export interface SmsSettings {
  enabled: boolean;
  provider: string;
  login: string | null;
  sender: string | null;
  baseUrl: string | null;
  hasPassword: boolean;
}

export interface NotificationSettings {
  channels: string[];
  copyToAdmin: boolean;
  publicBaseUrl: string | null;
  telegramFormat: string;
  emailFormat: string;
}

export interface Settings {
  telegram: TelegramSettings;
  email: EmailSettings;
  sms: SmsSettings;
  notification: NotificationSettings;
}

export interface ReceiptSettings {
  headerText: string | null;
  footerText: string | null;
  paperWidth: number;
}

@Injectable({ providedIn: 'root' })
export class LicenseApi {
  private readonly http = inject(HttpClient);

  get(): Observable<LicenseStatus> {
    return this.http.get<LicenseStatus>('/api/license');
  }

  options(): Observable<LicenseOptions> {
    return this.http.get<LicenseOptions>('/api/license/options');
  }

  update(body: { tariff: string; expiresAt: string | null; enabledFeatures: string | null }): Observable<void> {
    return this.http.put<void>('/api/license', body);
  }
}

@Injectable({ providedIn: 'root' })
export class SettingsApi {
  private readonly http = inject(HttpClient);

  allFeatures(): Observable<Feature[]> {
    return this.http.get<Feature[]>('/api/features');
  }

  get(): Observable<Settings> {
    return this.http.get<Settings>('/api/settings');
  }

  updateTelegram(body: { enabled: boolean; chatId: string | null; botToken: string | null }): Observable<void> {
    return this.http.put<void>('/api/settings/telegram', body);
  }

  updateEmail(body: {
    enabled: boolean;
    host: string | null;
    port: number;
    useSsl: boolean;
    username: string | null;
    password: string | null;
    fromAddress: string | null;
    fromName: string | null;
  }): Observable<void> {
    return this.http.put<void>('/api/settings/email', body);
  }

  updateSms(body: {
    enabled: boolean;
    provider: string;
    login: string | null;
    password: string | null;
    sender: string | null;
    baseUrl: string | null;
  }): Observable<void> {
    return this.http.put<void>('/api/settings/sms', body);
  }

  updateNotification(body: NotificationSettings): Observable<void> {
    return this.http.put<void>('/api/settings/notification', body);
  }

  receipt(): Observable<ReceiptSettings> {
    return this.http.get<ReceiptSettings>('/api/settings/receipt');
  }

  updateReceipt(body: ReceiptSettings): Observable<void> {
    return this.http.put<void>('/api/settings/receipt', body);
  }
}
