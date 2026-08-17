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
  paperFormat: string;
}

export interface SalesPolicy {
  shiftPolicy: string;
  maxDiscountPercent: number;
  maxDebtWriteOffAmount: number;
  maxDebtWriteOffPercent: number;
  defaultMinStock: number;
  staleRateDays: number;
  allowDebtSales: boolean;
  allowCustomerCredit: boolean;
  requireDebtDueDate: boolean;
  requireSupplier: boolean;
  showOutOfStock: boolean;
  showUnlistedProducts: boolean;
  allowInsufficientStockSales: boolean;
  allowRetroactiveCashback: boolean;
  saleCorrectionWindow: string;
  saleCorrectionDays: number;
  allowDebtWriteOff: boolean;
  printMoneyDocuments: boolean;
  allowConsolidatedAct: boolean;
  allowCustomerLoans: boolean;
  maxCustomerLoan: number;
  updateCatalogPriceOnSale: boolean;
  maxPriceIncreasePercent: number;
  customerRequirement: string;
  allowReturnOnVoidedSale: boolean;
  allowFreeReturnLines: boolean;
  requireReturnReason: boolean;
}

export interface LoginMethods {
  qrEnabled: boolean;
  qrRefreshSeconds: number;
  keyEnabled: boolean;
}

export interface StorageSettings {
  enabled: boolean;
  provider: string;
  endpoint: string | null;
  accessKey: string | null;
  bucket: string | null;
  useSsl: boolean;
  hasSecretKey: boolean;
  secretKeyLength: number;
}

export interface BarcodeLabelSettings {
  defaultWithPrice: boolean;
  allowPriceOverride: boolean;
  showSku: boolean;
  nameLines: number;
  currencyDisplay: 'symbol' | 'code';
  currencyCase: 'original' | 'upper' | 'lower';
  priceCurrencyMode: 'product' | 'default';
}

export interface ReminderSettings {
  enabled: boolean;
  minDaysOverdue: number;
  repeatEveryDays: number;
  minBalance: number;
  sendHourLocal: number;
  notifyBeforeDue: boolean;
  daysBeforeDue: number;
  notifyOnDueDate: boolean;
  channels: string[];
  overdueTemplate: string | null;
  dueSoonTemplate: string | null;
  dueTodayTemplate: string | null;
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

  storage(): Observable<StorageSettings> {
    return this.http.get<StorageSettings>('/api/settings/storage');
  }

  updateStorage(body: {
    enabled: boolean;
    provider: string;
    endpoint: string | null;
    accessKey: string | null;
    secretKey: string | null;
    bucket: string | null;
    useSsl: boolean;
  }): Observable<void> {
    return this.http.put<void>('/api/settings/storage', body);
  }

  salesPolicy(): Observable<SalesPolicy> {
    return this.http.get<SalesPolicy>('/api/settings/sales-policy');
  }

  updateSalesPolicy(body: SalesPolicy): Observable<void> {
    return this.http.put<void>('/api/settings/sales-policy', body);
  }

  loginMethods(): Observable<LoginMethods> {
    return this.http.get<LoginMethods>('/api/settings/login-methods');
  }

  updateLoginMethods(body: LoginMethods): Observable<void> {
    return this.http.put<void>('/api/settings/login-methods', body);
  }

  receipt(): Observable<ReceiptSettings> {
    return this.http.get<ReceiptSettings>('/api/settings/receipt');
  }

  updateReceipt(body: ReceiptSettings): Observable<void> {
    return this.http.put<void>('/api/settings/receipt', body);
  }

  barcodeLabel(): Observable<BarcodeLabelSettings> {
    return this.http.get<BarcodeLabelSettings>('/api/settings/barcode-label');
  }

  updateBarcodeLabel(body: BarcodeLabelSettings): Observable<void> {
    return this.http.put<void>('/api/settings/barcode-label', body);
  }

  reminder(): Observable<ReminderSettings> {
    return this.http.get<ReminderSettings>('/api/settings/reminder');
  }

  updateReminder(body: ReminderSettings): Observable<void> {
    return this.http.put<void>('/api/settings/reminder', body);
  }
}
