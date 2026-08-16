import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { Partner, PartnerSpecialty, PartnersApi } from '../../core/api/partners.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-partners',
  imports: [
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    TranslocoModule,
    PageHeader,
  ],
  templateUrl: './partners.html',
  styleUrl: './partners.scss',
})
export class Partners implements OnInit {
  private readonly api = inject(PartnersApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);

  readonly canEdit = this.auth.hasPermission('partners.edit');
  readonly canPublish = this.auth.hasPermission('partners.publish');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly partners = signal<Partner[]>([]);
  readonly specialties = signal<PartnerSpecialty[]>([]);
  readonly selected = signal<Partner | null>(null);

  // "Granted" is the only state that lets anything be published; the server enforces the same.
  readonly consentStates = ['NotAsked', 'Granted', 'Declined', 'Withdrawn'];

  search = '';
  editing: Partner | null = null;
  form = { fullName: '', phone: '', address: '', note: '', isEnabled: true, specialtyIds: [] as number[] };
  publicity = { consent: 'NotAsked', publicVisible: false, publicPhoneVisible: false, publicDisplayName: '', publicAbout: '' };

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.specialties.set(await lastValueFrom(this.api.specialties(true)));
      const list = await lastValueFrom(this.api.list(this.search.trim() || undefined));
      this.partners.set(list);
      const keep = this.selected()?.id;
      this.selected.set(list.find((p) => p.id === keep) ?? list[0] ?? null);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  startEdit(partner: Partner | null, tpl: unknown): void {
    if (!this.canEdit) return;
    this.editing = partner;
    this.form = {
      fullName: partner?.fullName ?? '',
      phone: partner?.phone ?? '',
      address: partner?.address ?? '',
      note: partner?.note ?? '',
      isEnabled: partner?.isEnabled ?? true,
      specialtyIds: (partner?.specialties ?? []).map((s) => s.id),
    };
    this.dialog.open(tpl as never, { width: '460px' });
  }

  async save(ref: MatDialogRef<unknown>, message: string): Promise<void> {
    if (!this.canEdit || !this.form.fullName.trim()) return;
    this.busy.set(true);
    try {
      const body = {
        fullName: this.form.fullName.trim(),
        phone: this.form.phone.trim() || null,
        address: this.form.address.trim() || null,
        note: this.form.note.trim() || null,
        isEnabled: this.form.isEnabled,
        specialtyIds: this.form.specialtyIds,
      };
      if (this.editing) await lastValueFrom(this.api.update(this.editing.id, body));
      else await lastValueFrom(this.api.create(body));
      ref.close();
      this.notify.success(message);
      await this.load();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  startPublicity(tpl: unknown): void {
    const partner = this.selected();
    if (!this.canPublish || !partner) return;
    this.publicity = {
      consent: partner.publicConsent || 'NotAsked',
      publicVisible: partner.publicVisible,
      publicPhoneVisible: partner.publicPhoneVisible,
      publicDisplayName: partner.publicDisplayName ?? '',
      publicAbout: partner.publicAbout ?? '',
    };
    this.dialog.open(tpl as never, { width: '460px' });
  }

  onConsentChange(): void {
    if (this.publicity.consent === 'Granted') return;
    this.publicity.publicVisible = false;
    this.publicity.publicPhoneVisible = false;
  }

  async savePublicity(ref: MatDialogRef<unknown>, message: string): Promise<void> {
    const partner = this.selected();
    if (!this.canPublish || !partner) return;
    this.busy.set(true);
    try {
      await lastValueFrom(
        this.api.setPublicity(partner.id, {
          consent: this.publicity.consent,
          publicVisible: this.publicity.publicVisible,
          publicPhoneVisible: this.publicity.publicPhoneVisible,
          publicDisplayName: this.publicity.publicDisplayName.trim() || null,
          publicAbout: this.publicity.publicAbout.trim() || null,
        }),
      );
      ref.close();
      this.notify.success(message);
      await this.load();
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  toggleSpecialty(id: number): void {
    const ids = this.form.specialtyIds;
    this.form.specialtyIds = ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id];
  }
}
