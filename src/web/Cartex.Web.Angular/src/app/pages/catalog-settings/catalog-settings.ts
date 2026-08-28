import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CatalogPack, CatalogSourceMode, SettingsApi } from '../../core/api/settings.api';
import { AuthService } from '../../core/auth.service';
import { CxDatePipe } from '../../core/format';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-catalog-settings',
  imports: [
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    TranslocoModule,
    CxDatePipe,
    PageHeader,
  ],
  templateUrl: './catalog-settings.html',
  styleUrl: './catalog-settings.scss',
})
export class CatalogSettingsPage implements OnInit {
  private readonly api = inject(SettingsApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);

  readonly canManage = inject(AuthService).hasPermission('settings.integrations');
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly pack = signal<CatalogPack | null>(null);
  readonly lastError = signal<string | null>(null);
  readonly mode = signal<CatalogSourceMode>('Online');
  readonly modes: CatalogSourceMode[] = ['Online', 'File', 'Off'];
  readonly modeHint = computed(() => `catalog_mode_${this.mode().toLowerCase()}_hint`);

  endpointBaseUrl = '';
  imageBaseUrl = '';
  packFile: File | null = null;
  manifestFile: File | null = null;

  async ngOnInit(): Promise<void> {
    try {
      const settings = await lastValueFrom(this.api.catalog());
      this.mode.set(settings.mode);
      this.endpointBaseUrl = settings.endpointBaseUrl;
      this.imageBaseUrl = settings.imageBaseUrl;
      this.pack.set(settings.pack);
      this.lastError.set(settings.lastError);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  onFile(input: HTMLInputElement, target: 'pack' | 'manifest'): void {
    const file = input.files?.[0] ?? null;
    if (target === 'pack') this.packFile = file;
    else this.manifestFile = file;
  }

  async save(): Promise<void> {
    if (!this.canManage) return;
    this.busy.set(true);
    try {
      await lastValueFrom(this.api.updateCatalog({
        mode: this.mode(),
        endpointBaseUrl: this.endpointBaseUrl.trim(),
        imageBaseUrl: this.imageBaseUrl.trim(),
      }));
      this.notify.success(this.transloco.translate('success'));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async uploadPack(): Promise<void> {
    if (!this.canManage || !this.packFile || !this.manifestFile) return;
    this.busy.set(true);
    try {
      this.pack.set(await lastValueFrom(this.api.uploadCatalogPack(this.packFile, this.manifestFile)));
      this.lastError.set(null);
      this.packFile = null;
      this.manifestFile = null;
      this.notify.success(this.transloco.translate('catalog_pack_uploaded'));
    } catch (e) {
      this.lastError.set(this.describe(e));
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  async deletePack(): Promise<void> {
    if (!this.canManage || !window.confirm(this.transloco.translate('catalog_pack_delete_confirm'))) return;
    this.busy.set(true);
    try {
      await lastValueFrom(this.api.deleteCatalogPack());
      this.pack.set(null);
      this.lastError.set(null);
      this.notify.success(this.transloco.translate('success'));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }

  private describe(error: unknown): string {
    const body = (error as { error?: { detail?: string; title?: string } } | null)?.error;
    return body?.detail || body?.title || this.transloco.translate('err_server_error');
  }
}
