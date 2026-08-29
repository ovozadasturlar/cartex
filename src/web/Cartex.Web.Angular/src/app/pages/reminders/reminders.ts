import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { ReminderSettings, SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

const EMPTY: ReminderSettings = {
  enabled: false,
  minDaysOverdue: 1,
  repeatEveryDays: 3,
  minBalance: 0,
  sendHourLocal: 10,
  notifyBeforeDue: true,
  daysBeforeDue: 1,
  notifyOnDueDate: true,
  channels: [],
  overdueTemplate: null,
  dueSoonTemplate: null,
  dueTodayTemplate: null,
};

@Component({
  selector: 'app-reminders',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    TranslocoModule,
    PageHeader,
  ],
  template: `
    <div class="page" *transloco="let t">
      <cx-page-header [title]="t('debt_reminders')" [subtitle]="t('reminder_page_hint')" />
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      } @else {
        <section class="cx-card form-card">
          <div class="switch-row">
            <div>
              <strong>{{ t('reminder_enabled') }}</strong>
              <p>{{ t('reminder_enabled_hint') }}</p>
            </div>
            <mat-slide-toggle [(ngModel)]="model.enabled" [disabled]="!canEdit" />
          </div>

          <div class="divider"></div>
          <h3>{{ t('schedule') }}</h3>
          <div class="form-grid">
            <mat-form-field appearance="outline">
              <mat-label>{{ t('reminder_min_days') }}</mat-label>
              <input matInput type="number" min="0" [(ngModel)]="model.minDaysOverdue" [disabled]="!canEdit" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ t('reminder_repeat_days') }}</mat-label>
              <input matInput type="number" min="1" [(ngModel)]="model.repeatEveryDays" [disabled]="!canEdit" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ t('reminder_min_balance') }}</mat-label>
              <input matInput type="number" min="0" [(ngModel)]="model.minBalance" [disabled]="!canEdit" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ t('reminder_send_hour') }}</mat-label>
              <input matInput type="number" min="0" max="23" [(ngModel)]="model.sendHourLocal" [disabled]="!canEdit" />
            </mat-form-field>
          </div>

          <div class="option-grid">
            <div class="option-card">
              <mat-checkbox [(ngModel)]="model.notifyBeforeDue" [disabled]="!canEdit">
                {{ t('notify_before_due') }}
              </mat-checkbox>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ t('days_before_due') }}</mat-label>
                <input matInput type="number" min="1" [(ngModel)]="model.daysBeforeDue"
                       [disabled]="!canEdit || !model.notifyBeforeDue" />
              </mat-form-field>
            </div>
            <div class="option-card">
              <mat-checkbox [(ngModel)]="model.notifyOnDueDate" [disabled]="!canEdit">
                {{ t('notify_on_due_date') }}
              </mat-checkbox>
            </div>
          </div>

          <h3>{{ t('channels') }}</h3>
          <div class="channels">
            @for (channel of channelOptions; track channel) {
              <mat-checkbox
                [checked]="model.channels.includes(channel)"
                [disabled]="!canEdit"
                (change)="toggleChannel(channel, $event.checked)">
                {{ channel }}
              </mat-checkbox>
            }
          </div>

          <h3>{{ t('message_templates') }}</h3>
          <div class="templates">
            <mat-form-field appearance="outline">
              <mat-label>{{ t('reminder_template') }}</mat-label>
              <textarea matInput rows="3" [(ngModel)]="model.overdueTemplate" [disabled]="!canEdit"></textarea>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ t('due_soon_template') }}</mat-label>
              <textarea matInput rows="3" [(ngModel)]="model.dueSoonTemplate" [disabled]="!canEdit"></textarea>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ t('due_today_template') }}</mat-label>
              <textarea matInput rows="3" [(ngModel)]="model.dueTodayTemplate" [disabled]="!canEdit"></textarea>
            </mat-form-field>
          </div>

          @if (canEdit) {
            <div class="actions">
              <button matButton="filled" [disabled]="saving()" (click)="save()">{{ t('save') }}</button>
            </div>
          }
        </section>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    .page { max-width: 900px; }
    .form-card { padding: 20px; }
    .switch-row { display: flex; align-items: center; justify-content: space-between; gap: 18px; }
    .switch-row p { margin: 4px 0 0; color: var(--cx-text-2); font-size: 12px; }
    .divider { height: 1px; margin: 18px 0; background: var(--cx-border); }
    h3 { margin: 18px 0 12px; font-size: 14px; }
    .form-grid, .option-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
    .option-card {
      display: flex; flex-direction: column; gap: 12px; padding: 14px;
      border: 1px solid var(--cx-border); border-radius: 10px; background: var(--cx-app-bg);
    }
    .channels { display: flex; flex-wrap: wrap; gap: 8px 20px; }
    .templates { display: grid; gap: 10px; }
    .actions { display: flex; justify-content: flex-end; margin-top: 14px; }
    @media (width <= 699px) {
      .form-card { padding: 16px; }
      .form-grid, .option-grid { grid-template-columns: 1fr; }
    }
  `,
})
export class Reminders implements OnInit {
  private readonly api = inject(SettingsApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly canEdit = inject(AuthService).hasPermission('notifications.edit');
  readonly channelOptions = ['Telegram', 'Sms', 'Email'];
  model: ReminderSettings = { ...EMPTY, channels: [] };

  async ngOnInit(): Promise<void> {
    try {
      this.model = await lastValueFrom(this.api.reminder());
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.loading.set(false);
    }
  }

  toggleChannel(channel: string, checked: boolean): void {
    if (!this.canEdit) return;
    this.model.channels = checked
      ? [...new Set([...this.model.channels, channel])]
      : this.model.channels.filter((item) => item !== channel);
  }

  async save(): Promise<void> {
    if (!this.canEdit || this.saving()) return;
    this.saving.set(true);
    try {
      await lastValueFrom(this.api.updateReminder(this.model));
      this.notify.success(this.transloco.translate('success'));
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.saving.set(false);
    }
  }
}
