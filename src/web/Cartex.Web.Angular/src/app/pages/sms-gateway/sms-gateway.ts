import { HttpClient } from '@angular/common/http';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { SettingsApi, SmsSettings } from '../../core/api/settings.api';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

interface SmsDevice {
  id: number; deviceName: string; simSlot: number; simOperator: string; phoneLabel: string | null;
  isTrusted: boolean; isConsented: boolean; isEnabled: boolean; monthlyQuota: number | null;
  quotaResetDay: number; sentThisPeriod: number; maxPerHour: number; minIntervalSeconds: number;
  priority: number; lastSeenAt: string | null; lastError: string | null; isOnline: boolean;
  isPaused: boolean; blockReason: string; quotaResetsAt: string; lowQuotaWarnPercent: number; isOverQuota: boolean;
  lastSentAt: string | null; linkedCustomerCount: number;
}

interface SmsJob {
  id: number; kind: string; phoneMasked: string; phone: string | null; text: string; status: string;
  assignedDeviceId: number | null; deviceLabel: string | null; segmentCount: number; createdAt: string;
  errorMessage: string | null; waitingReason: string | null; customerName: string | null; customerId: number | null;
  selected?: boolean;
}

interface SmsPeriod { sent: number; waiting: number; failed: number; }
interface SmsJournal { jobs: SmsJob[]; summary: { today: SmsPeriod; thisMonth: SmsPeriod }; }

@Component({
  selector: 'app-sms-gateway',
  imports: [DatePipe, DecimalPipe, FormsModule, MatButtonModule, MatIconModule, MatProgressBarModule, MatSlideToggleModule, TranslocoModule, PageHeader],
  template: `
    <ng-container *transloco="let t">
      <cx-page-header [title]="t('sms_gateway')" [subtitle]="t('sms_gateway_sender_warning')">
        <button matButton="outlined" (click)="load()"><mat-icon>refresh</mat-icon>{{ t('refresh') }}</button>
        <button matButton="filled" (click)="saveSettings()"><mat-icon>save</mat-icon>{{ t('save') }}</button>
      </cx-page-header>
      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      <div class="content">
        @if (settings.testMode) { <section class="cx-card test-banner">{{ t('sms_gateway_test_mode_banner') }}</section> }
        <section class="cx-card warning"><strong>{{ t('sms_gateway_operator_warning') }}</strong><span>{{ t('sms_gateway_fallback_warning') }}</span></section>
        <section class="cx-card panel">
          <h2>{{ t('sms_gateway_policy') }}</h2>
          <mat-slide-toggle [(ngModel)]="settings.enabled">{{ t('enabled') }}</mat-slide-toggle>
          <mat-slide-toggle [(ngModel)]="settings.testMode">{{ t('sms_gateway_test_mode') }}</mat-slide-toggle>
          <small>{{ t('sms_gateway_test_mode_hint') }}</small>
          <label>{{ t('sms_gateway_allowed_numbers') }}<textarea [(ngModel)]="allowedNumbersText"></textarea></label>
          <div class="fields three">
            <label>{{ t('provider') }}<select [(ngModel)]="settings.provider"><option value="device">device</option><option value="eskiz">eskiz</option><option value="playmobile">playmobile</option></select></label>
            <label>{{ t('sms_gateway_fallback') }}<select [(ngModel)]="settings.fallbackProvider"><option value="none">none</option><option value="eskiz">eskiz</option><option value="playmobile">playmobile</option></select></label>
            <label>{{ t('sms_gateway_fallback_minutes') }}<input type="number" min="1" [(ngModel)]="settings.fallbackAfterMinutes" /></label>
          </div>
          <mat-slide-toggle [(ngModel)]="settings.quietHoursEnabled">{{ t('sms_gateway_quiet_hours') }}</mat-slide-toggle>
          <div class="fields three">
            <label>{{ t('sms_gateway_send_window_start') }}<input type="time" [(ngModel)]="settings.sendWindowStart" /></label>
            <label>{{ t('sms_gateway_send_window_end') }}<input type="time" [(ngModel)]="settings.sendWindowEnd" /></label>
          </div>
          <h2>{{ t('sms_gateway_sticky_waits') }}</h2>
          <div class="fields four">
            <label>{{ t('sms_gateway_debt') }}<input type="number" min="0" max="1440" [(ngModel)]="settings.debtReminderStickyWaitMinutes" /></label>
            <label>{{ t('sms_gateway_receipt') }}<input type="number" min="0" max="1440" [(ngModel)]="settings.receiptLinkStickyWaitMinutes" /></label>
            <label>{{ t('sms_gateway_promotion') }}<input type="number" min="0" max="1440" [(ngModel)]="settings.promotionStickyWaitMinutes" /></label>
            <label>{{ t('sms_gateway_manual') }}<input type="number" min="0" max="1440" [(ngModel)]="settings.manualStickyWaitMinutes" /></label>
          </div>
          <h2>{{ t('sms_gateway_templates') }}</h2>
          <div class="templates">
            <label><mat-slide-toggle [(ngModel)]="settings.debtReminderEnabled">{{ t('sms_gateway_debt') }}</mat-slide-toggle><textarea [(ngModel)]="settings.debtReminderTemplate"></textarea></label>
            <label><mat-slide-toggle [(ngModel)]="settings.receiptLinkEnabled">{{ t('sms_gateway_receipt') }}</mat-slide-toggle><textarea [(ngModel)]="settings.receiptLinkTemplate"></textarea></label>
            <label><mat-slide-toggle [(ngModel)]="settings.sendReceiptOnSale">{{ t('send_receipt_on_sale') }}</mat-slide-toggle><small>{{ t('send_receipt_on_sale_hint') }}</small></label>
            <label><mat-slide-toggle [(ngModel)]="settings.promotionEnabled">{{ t('sms_gateway_promotion') }}</mat-slide-toggle><textarea [(ngModel)]="settings.promotionTemplate"></textarea></label>
            <label><mat-slide-toggle [(ngModel)]="settings.manualEnabled">{{ t('sms_gateway_manual') }}</mat-slide-toggle><textarea [(ngModel)]="settings.manualTemplate"></textarea></label>
          </div>
        </section>
        <section class="cx-card panel">
          <h2>{{ t('sms_gateway_devices') }}</h2>
          @for (device of devices(); track device.id) {
            <article class="device">
              <header><strong>{{ device.deviceName }}</strong><span>{{ t('sms_gateway_status_' + device.blockReason) }}</span></header>
              <small>SIM {{ device.simSlot + 1 }} · {{ device.simOperator }} · {{ device.phoneLabel || '—' }}</small>
              <div class="quota-head"><strong>{{ device.sentThisPeriod | number }} / {{ device.monthlyQuota === null ? t('unlimited') : (device.monthlyQuota | number) }}</strong><small>{{ device.isOverQuota ? t('sms_gateway_over_quota', { days: daysLeft(device) }) : t('sms_gateway_days_left', { days: daysLeft(device) }) }}</small></div>
              @if (device.monthlyQuota !== null) { <mat-progress-bar mode="determinate" [value]="quotaProgress(device)" [class]="quotaLevel(device)" /> }
              <small>{{ t('consent') }}: {{ t(device.isConsented ? 'consent_granted' : 'consent_notasked') }} · {{ t('sms_gateway_owner_readonly') }}</small>
              <div class="metrics"><span>{{ t('sms_gateway_remaining') }} <strong>{{ device.monthlyQuota === null ? '∞' : (remaining(device) | number) }}</strong></span><span>{{ t('sms_gateway_last_sent') }} <strong>{{ device.lastSentAt ? (device.lastSentAt | date:'dd.MM.yyyy HH:mm') : '—' }}</strong></span><span>{{ t('sms_gateway_linked_customers') }} <strong>{{ device.linkedCustomerCount | number }}</strong></span></div>
              <div class="fields one">
                <label>{{ t('priority') }}<input type="number" [(ngModel)]="device.priority" /></label>
              </div>
              @if (device.lastError) { <small class="error">{{ device.lastError }}</small> }
              <footer><mat-slide-toggle [(ngModel)]="device.isEnabled">{{ t('enabled') }}</mat-slide-toggle><button matButton="outlined" (click)="trust(device)">{{ t('sms_gateway_trust') }}</button><button matButton="filled" (click)="saveDevice(device)">{{ t('save') }}</button></footer>
            </article>
          } @empty { <p class="muted">{{ t('no_data') }}</p> }
        </section>
        <section class="cx-card panel">
          <h2>{{ t('sms_journal') }}</h2>
          <div class="summary">
            <article><strong>{{ t('today') }}</strong><span>{{ t('sms_gateway_summary_sent') }}: {{ summary().today.sent }}</span><span>{{ t('sms_gateway_summary_waiting') }}: {{ summary().today.waiting }}</span><span>{{ t('sms_gateway_summary_failed') }}: {{ summary().today.failed }}</span></article>
            <article><strong>{{ t('this_month') }}</strong><span>{{ t('sms_gateway_summary_sent') }}: {{ summary().thisMonth.sent }}</span><span>{{ t('sms_gateway_summary_waiting') }}: {{ summary().thisMonth.waiting }}</span><span>{{ t('sms_gateway_summary_failed') }}: {{ summary().thisMonth.failed }}</span></article>
          </div>
          <div class="fields four">
            <label>{{ t('status') }}<select [(ngModel)]="filterStatus"><option value=""></option>@for (status of statuses; track status) { <option [value]="status">{{ status }}</option> }</select></label>
            <label>{{ t('type') }}<select [(ngModel)]="filterKind"><option value=""></option>@for (kind of kinds; track kind) { <option [value]="kind">{{ kind }}</option> }</select></label>
            <label>{{ t('device') }}<select [(ngModel)]="filterDeviceId"><option [ngValue]="null"></option>@for (device of devices(); track device.id) { <option [ngValue]="device.id">SIM {{ device.simSlot + 1 }} · {{ device.simOperator }}</option> }</select></label>
            <label>{{ t('customer') }}<input type="number" [(ngModel)]="filterCustomerId" /></label>
            <label>{{ t('from') }}<input type="date" [(ngModel)]="filterFrom" /></label>
            <label>{{ t('to') }}<input type="date" [(ngModel)]="filterTo" /></label>
            <button matButton="outlined" (click)="load()">{{ t('filter') }}</button>
          </div>
          <div class="actions"><button matButton="outlined" (click)="retrySelected()">{{ t('sms_gateway_retry') }}</button><button matButton="outlined" (click)="cancelSelected()">{{ t('cancel') }}</button><select [(ngModel)]="reassignDeviceId"><option [ngValue]="null"></option>@for (device of devices(); track device.id) { <option [ngValue]="device.id">SIM {{ device.simSlot + 1 }} · {{ device.simOperator }}</option> }</select><button matButton="outlined" (click)="reassignSelected()">{{ t('sms_gateway_reassign') }}</button></div>
          <div class="jobs">
            @for (job of jobs(); track job.id) {
              <article><header><input type="checkbox" [(ngModel)]="job.selected" /><strong>{{ job.kind }}</strong><span>{{ job.status }}</span><small>{{ job.createdAt | date:'dd.MM.yyyy HH:mm' }}</small></header><p>{{ job.phone || job.phoneMasked }} · {{ job.text }}</p><small>{{ job.customerName || job.customerId || '—' }} · {{ job.deviceLabel || '—' }} · {{ t('sms_segments') }}: {{ job.segmentCount }}</small>@if (job.waitingReason) { <small>{{ t('sms_gateway_wait_' + job.waitingReason) }}</small> }@if (job.errorMessage) { <small class="error">{{ job.errorMessage }}</small> }</article>
            } @empty { <p class="muted">{{ t('no_data') }}</p> }
          </div>
        </section>
      </div>
    </ng-container>
  `,
  styles: `
    :host { display:block; height:100%; overflow-y:auto; } .content { display:grid; gap:14px; padding-bottom:24px; max-width:1100px; }
    .panel,.warning,.test-banner { padding:18px; display:grid; gap:14px; } .warning { color:var(--cx-danger); } .test-banner { color:var(--cx-warning); font-weight:700; } h2 { margin:0; font-size:16px; }
    .fields { display:grid; gap:10px; } .three { grid-template-columns:repeat(3,1fr); } .four { grid-template-columns:repeat(4,1fr); }
    label { display:flex; flex-direction:column; gap:5px; color:var(--cx-text-2); font-size:12px; } input,select,textarea { min-height:40px; border:1px solid var(--cx-border); border-radius:8px; padding:8px; background:var(--cx-surface); color:var(--cx-text); }
    textarea { min-height:70px; resize:vertical; } .templates { display:grid; grid-template-columns:1fr 1fr; gap:12px; } .device,.jobs article { border:1px solid var(--cx-border); border-radius:10px; padding:12px; display:grid; gap:9px; }
    header,footer { display:flex; align-items:center; gap:10px; } header span { margin-left:auto; } header small { color:var(--cx-text-3); } footer { justify-content:flex-end; } footer mat-slide-toggle { margin-right:auto; }
    small,.muted { color:var(--cx-text-3); } .error { color:var(--cx-danger); } .jobs { display:grid; gap:8px; max-height:520px; overflow:auto; } .jobs p { margin:0; }
    .quota-head { display:flex; justify-content:space-between; gap:12px; } .one { grid-template-columns:minmax(180px, 320px); }
    .metrics,.actions { display:flex; gap:16px; align-items:center; flex-wrap:wrap; } .summary { display:grid; grid-template-columns:1fr 1fr; gap:10px; } .summary article { display:grid; gap:4px; border:1px solid var(--cx-border); border-radius:10px; padding:12px; }
    mat-progress-bar.success { --mat-progress-bar-active-indicator-color:var(--cx-success); } mat-progress-bar.warning { --mat-progress-bar-active-indicator-color:var(--cx-warning); } mat-progress-bar.orange { --mat-progress-bar-active-indicator-color:var(--cx-warning); filter:saturate(1.5) brightness(.85); } mat-progress-bar.danger { --mat-progress-bar-active-indicator-color:var(--cx-danger); }
    @media(max-width:760px){ .three,.four,.templates,.summary { grid-template-columns:1fr; } footer { flex-wrap:wrap; } }
  `,
})
export class SmsGateway implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly settingsApi = inject(SettingsApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  readonly loading = signal(true);
  readonly devices = signal<SmsDevice[]>([]);
  readonly jobs = signal<SmsJob[]>([]);
  readonly summary = signal<{ today: SmsPeriod; thisMonth: SmsPeriod }>({ today: { sent: 0, waiting: 0, failed: 0 }, thisMonth: { sent: 0, waiting: 0, failed: 0 } });
  readonly statuses = ['Pending', 'Assigned', 'Sent', 'Delivered', 'Failed', 'Simulated', 'Cancelled'];
  readonly kinds = ['DebtReminder', 'ReceiptLink', 'Promotion', 'Manual'];
  private branchId = 0;
  allowedNumbersText = '';
  filterStatus = ''; filterKind = ''; filterDeviceId: number | null = null; filterCustomerId: number | null = null; filterFrom = ''; filterTo = ''; reassignDeviceId: number | null = null;
  settings: SmsSettings = { enabled: false, provider: 'device', login: null, sender: null, baseUrl: null, hasPassword: false, fallbackProvider: 'none', fallbackAfterMinutes: 30, debtReminderEnabled: true, receiptLinkEnabled: false, promotionEnabled: false, manualEnabled: true, sendReceiptOnSale: false, debtReminderTemplate: '', receiptLinkTemplate: '', promotionTemplate: '', manualTemplate: '', testMode: true, testAllowedNumbers: [], debtReminderStickyWaitMinutes: 15, receiptLinkStickyWaitMinutes: 0, promotionStickyWaitMinutes: 60, manualStickyWaitMinutes: 0, quietHoursEnabled: true, sendWindowStart: '09:00', sendWindowEnd: '21:00' };

  async ngOnInit(): Promise<void> { await this.load(); }
  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const context = await lastValueFrom(this.http.get<{ defaultBranchId: number; branches: { id: number }[] }>('/api/auth/context'));
      this.branchId = context.defaultBranchId || context.branches[0]?.id || 0;
      if (!this.branchId) return;
      const params: Record<string, string | number> = { branchId: this.branchId, take: 100 };
      if (this.filterStatus) params['status'] = this.filterStatus;
      if (this.filterKind) params['kind'] = this.filterKind;
      if (this.filterDeviceId) params['deviceId'] = this.filterDeviceId;
      if (this.filterCustomerId) params['customerId'] = this.filterCustomerId;
      if (this.filterFrom) params['from'] = this.filterFrom;
      if (this.filterTo) {
        const to = new Date(`${this.filterTo}T00:00:00`);
        to.setDate(to.getDate() + 1);
        params['to'] = to.toISOString();
      }
      const [settings, devices, journal] = await Promise.all([
        lastValueFrom(this.settingsApi.get()),
        lastValueFrom(this.http.get<SmsDevice[]>('/api/sms-gateway/devices', { params: { branchId: this.branchId } })),
        lastValueFrom(this.http.get<SmsJournal>('/api/sms-gateway/journal', { params })),
      ]);
      this.settings = settings.sms; this.allowedNumbersText = settings.sms.testAllowedNumbers.join('\n'); this.devices.set(devices); this.jobs.set(journal.jobs); this.summary.set(journal.summary);
    } catch (error) { this.notify.error(error); } finally { this.loading.set(false); }
  }
  async saveSettings(): Promise<void> {
    const testAllowedNumbers = this.allowedNumbersText.split(/[\n,;]+/).map((x) => x.trim()).filter((x) => x.length > 0);
    try { await lastValueFrom(this.settingsApi.updateSms({ ...this.settings, testAllowedNumbers, password: null })); this.notify.success(this.transloco.translate('success')); }
    catch (error) { this.notify.error(error); }
  }
  async saveDevice(device: SmsDevice): Promise<void> {
    const body = { isEnabled: device.isEnabled, priority: device.priority };
    try { await lastValueFrom(this.http.put(`/api/sms-gateway/devices/${device.id}`, body)); this.notify.success(this.transloco.translate('success')); await this.load(); }
    catch (error) { this.notify.error(error); }
  }
  async trust(device: SmsDevice): Promise<void> {
    try { await lastValueFrom(this.http.put(`/api/sms-gateway/devices/${device.id}/trust`, { isTrusted: !device.isTrusted })); await this.load(); }
    catch (error) { this.notify.error(error); }
  }
  async retrySelected(): Promise<void> { await this.jobAction('/api/sms-gateway/jobs/retry', { jobIds: this.selectedIds() }); }
  async cancelSelected(): Promise<void> { await this.jobAction('/api/sms-gateway/jobs/cancel', { jobIds: this.selectedIds() }); }
  async reassignSelected(): Promise<void> { if (this.reassignDeviceId) await this.jobAction('/api/sms-gateway/jobs/reassign', { jobIds: this.selectedIds(), deviceId: this.reassignDeviceId }); }
  private selectedIds(): number[] { return this.jobs().filter((job) => job.selected).map((job) => job.id); }
  private async jobAction(url: string, body: object): Promise<void> { if (this.selectedIds().length === 0) return; try { await lastValueFrom(this.http.post<void>(url, body)); await this.load(); } catch (error) { this.notify.error(error); } }
  remaining(device: SmsDevice): number { return device.monthlyQuota === null ? 0 : Math.max(0, device.monthlyQuota - device.sentThisPeriod); }
  quotaProgress(device: SmsDevice): number { return device.monthlyQuota && device.monthlyQuota > 0 ? Math.min(100, device.sentThisPeriod * 100 / device.monthlyQuota) : 100; }
  quotaLevel(device: SmsDevice): string {
    if (device.monthlyQuota === null) return 'success';
    const remaining = device.monthlyQuota <= 0 ? 0 : Math.max(0, (device.monthlyQuota - device.sentThisPeriod) * 100 / device.monthlyQuota);
    if (remaining >= 50) return 'success';
    if (remaining >= 25) return 'warning';
    if (remaining >= 10) return 'orange';
    return 'danger';
  }
  daysLeft(device: SmsDevice): number { return Math.max(0, Math.ceil((new Date(device.quotaResetsAt).getTime() - Date.now()) / 86_400_000)); }
}
