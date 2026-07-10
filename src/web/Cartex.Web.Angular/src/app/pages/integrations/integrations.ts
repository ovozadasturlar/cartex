import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule } from '@jsverse/transloco';
import { Observable, lastValueFrom } from 'rxjs';
import { Settings, SettingsApi, StorageSettings } from '../../core/api/settings.api';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';
import { Subnav, SubnavItem } from '../../shared/subnav';

@Component({
  selector: 'app-integrations',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    TranslocoModule,
    PageHeader,
    Subnav,
  ],
  templateUrl: './integrations.html',
  styleUrl: './integrations.scss',
})
export class Integrations implements OnInit {
  private readonly api = inject(SettingsApi);
  private readonly notify = inject(NotifyService);

  readonly loading = signal(true);
  readonly section = signal('telegram');

  sections(t: (key: string) => string): SubnavItem[] {
    return [
      { key: 'telegram', label: 'Telegram' },
      { key: 'email', label: 'Email (SMTP)' },
      { key: 'sms', label: 'SMS' },
      { key: 'receipt', label: t('receipt_delivery') },
      { key: 'storage', label: t('storage_settings') },
    ];
  }
  readonly busy = signal(false);
  readonly smsProviders = ['eskiz', 'playmobile'];
  readonly receiptFormats = ['Auto', 'Link', 'Pdf', 'Text'];

  tgEnabled = false;
  tgHasToken = false;
  tgToken = '';
  tgChatId = '';

  emailEnabled = false;
  emailHost = '';
  emailPort = 465;
  emailUseSsl = true;
  emailUsername = '';
  emailPassword = '';
  emailFromAddress = '';
  emailFromName = '';

  readonly smtpPresets = [
    { name: 'Gmail', host: 'smtp.gmail.com', port: 587, ssl: true },
    { name: 'Yandex', host: 'smtp.yandex.ru', port: 465, ssl: true },
    { name: 'Mail.ru', host: 'smtp.mail.ru', port: 465, ssl: true },
    { name: 'Outlook', host: 'smtp.office365.com', port: 587, ssl: true },
    { name: 'Yahoo', host: 'smtp.mail.yahoo.com', port: 465, ssl: true },
    { name: 'Boshqa', host: null, port: 587, ssl: true },
  ];
  smtpPreset: { name: string; host: string | null; port: number; ssl: boolean } | null = null;

  onSmtpPreset(): void {
    const p = this.smtpPreset;
    if (!p) return;
    if (p.host) this.emailHost = p.host;
    this.emailPort = p.port;
    this.emailUseSsl = p.ssl;
  }

  smsEnabled = false;
  smsProvider = 'eskiz';
  smsLogin = '';
  smsPassword = '';
  smsSender = '';
  smsBaseUrl = '';

  storageEnabled = false;
  storageProvider = 'local';
  storageEndpoint = '';
  storageAccessKey = '';
  storageSecretKey = '';
  storageBucket = '';
  storageUseSsl = false;
  storageHasSecret = false;

  channelTelegram = false;
  channelSms = false;
  channelEmail = false;
  copyToAdmin = false;
  publicBaseUrl = '';
  telegramFormat = 'Auto';
  emailFormat = 'Auto';

  async ngOnInit(): Promise<void> {
    try {
      this.apply(await lastValueFrom(this.api.get()));
      this.applyStorage(await lastValueFrom(this.api.storage()));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  saveTelegram(message: string): Promise<void> {
    return this.run(
      this.api.updateTelegram({
        enabled: this.tgEnabled,
        chatId: this.tgChatId.trim() || null,
        botToken: this.tgToken.trim() || null,
      }),
      message,
    );
  }

  saveEmail(message: string): Promise<void> {
    return this.run(
      this.api.updateEmail({
        enabled: this.emailEnabled,
        host: this.emailHost.trim() || null,
        port: this.emailPort,
        useSsl: this.emailUseSsl,
        username: this.emailUsername.trim() || null,
        password: this.emailPassword || null,
        fromAddress: this.emailFromAddress.trim() || null,
        fromName: this.emailFromName.trim() || null,
      }),
      message,
    );
  }

  saveSms(message: string): Promise<void> {
    return this.run(
      this.api.updateSms({
        enabled: this.smsEnabled,
        provider: this.smsProvider,
        login: this.smsLogin.trim() || null,
        password: this.smsPassword || null,
        sender: this.smsSender.trim() || null,
        baseUrl: this.smsBaseUrl.trim() || null,
      }),
      message,
    );
  }

  async saveStorage(message: string): Promise<void> {
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.updateStorage({
          enabled: this.storageEnabled,
          provider: this.storageProvider,
          endpoint: this.storageEndpoint.trim() || null,
          accessKey: this.storageAccessKey.trim() || null,
          secretKey: this.storageSecretKey || null,
          bucket: this.storageBucket.trim() || null,
          useSsl: this.storageUseSsl,
        }),
      );
      this.applyStorage(await lastValueFrom(this.api.storage()));
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  private applyStorage(s: StorageSettings): void {
    this.storageEnabled = s.enabled;
    this.storageProvider = s.provider || 'local';
    this.storageEndpoint = s.endpoint ?? '';
    this.storageAccessKey = s.accessKey ?? '';
    this.storageSecretKey = '';
    this.storageBucket = s.bucket ?? '';
    this.storageUseSsl = s.useSsl;
    this.storageHasSecret = s.hasSecretKey;
  }

  saveNotification(message: string): Promise<void> {
    const channels: string[] = [];
    if (this.channelTelegram) channels.push('Telegram');
    if (this.channelSms) channels.push('Sms');
    if (this.channelEmail) channels.push('Email');
    return this.run(
      this.api.updateNotification({
        channels,
        copyToAdmin: this.copyToAdmin,
        publicBaseUrl: this.publicBaseUrl.trim() || null,
        telegramFormat: this.telegramFormat,
        emailFormat: this.emailFormat,
      }),
      message,
    );
  }

  private async run(request: Observable<void>, message: string): Promise<void> {
    this.busy.set(true);
    try {
      await lastValueFrom(request);
      this.apply(await lastValueFrom(this.api.get()));
      this.notify.success(message);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  private apply(s: Settings): void {
    this.tgEnabled = s.telegram.enabled;
    this.tgHasToken = s.telegram.hasBotToken;
    this.tgToken = '';
    this.tgChatId = s.telegram.chatId ?? '';

    this.emailEnabled = s.email.enabled;
    this.emailHost = s.email.host ?? '';
    this.emailPort = s.email.port;
    this.emailUseSsl = s.email.useSsl;
    this.emailUsername = s.email.username ?? '';
    this.emailPassword = '';
    this.emailFromAddress = s.email.fromAddress ?? '';
    this.emailFromName = s.email.fromName ?? '';

    this.smsEnabled = s.sms.enabled;
    this.smsProvider = s.sms.provider || 'eskiz';
    this.smsLogin = s.sms.login ?? '';
    this.smsPassword = '';
    this.smsSender = s.sms.sender ?? '';
    this.smsBaseUrl = s.sms.baseUrl ?? '';

    this.channelTelegram = s.notification.channels.includes('Telegram');
    this.channelSms = s.notification.channels.includes('Sms');
    this.channelEmail = s.notification.channels.includes('Email');
    this.copyToAdmin = s.notification.copyToAdmin;
    this.publicBaseUrl = s.notification.publicBaseUrl ?? '';
    this.telegramFormat = s.notification.telegramFormat || 'Auto';
    this.emailFormat = s.notification.emailFormat || 'Auto';
  }
}
