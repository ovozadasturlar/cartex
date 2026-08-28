import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { Manufacturer, ManufacturersApi } from '../../core/api/catalog.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-manufacturers',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    TranslocoModule,
    EmptyState,
    PageHeader,
  ],
  templateUrl: './manufacturers.html',
  styleUrl: './manufacturers.scss',
})
export class Manufacturers implements OnInit {
  private readonly api = inject(ManufacturersApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);

  private readonly auth = inject(AuthService);
  readonly canCreate = this.auth.hasPermission('manufacturers.create');
  readonly canEdit = this.auth.hasPermission('manufacturers.edit');
  readonly canDelete = this.auth.hasPermission('manufacturers.delete');
  readonly loading = signal(true);
  readonly all = signal<Manufacturer[]>([]);
  readonly search = signal('');
  readonly items = computed(() => {
    const q = this.search().toLowerCase();
    return q ? this.all().filter((m) => m.name.toLowerCase().includes(q)) : this.all();
  });
  readonly columns = ['name', 'actions'];

  ngOnInit(): void {
    void this.load();
  }

  openCreate(): void {
    if (!this.canCreate) return;
    this.openDialog(null);
  }

  openEdit(manufacturer: Manufacturer): void {
    if (this.canEdit) this.openDialog(manufacturer);
  }

  remove(manufacturer: Manufacturer, event: Event): void {
    event.stopPropagation();
    if (!this.canDelete) return;
    this.dialog
      .open(ConfirmDialog, { data: manufacturer.name, width: '360px', maxWidth: '94vw', autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe(async (confirmed) => {
        if (!confirmed) return;
        try {
          await lastValueFrom(this.api.delete(manufacturer.id));
          await this.load();
        } catch (e) {
          this.notify.error(e);
        }
      });
  }

  private openDialog(manufacturer: Manufacturer | null): void {
    this.dialog
      .open(ManufacturerDialog, { data: manufacturer, width: '400px', maxWidth: '94vw', autoFocus: 'first-tabbable' })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) void this.load();
      });
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.all.set(await lastValueFrom(this.api.all()));
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.loading.set(false);
    }
  }
}

@Component({
  selector: 'app-manufacturer-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, TranslocoModule],
  styleUrl: './manufacturers.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ manufacturer ? t('edit') : t('add') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-body">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('name') }}</mat-label>
          <input matInput cdkFocusInitial [(ngModel)]="name" (keydown.enter)="save()" />
        </mat-form-field>
      </div>
      <div mat-dialog-actions align="end">
        <button mat-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button [disabled]="busy() || !name.trim()" (click)="save()">{{ t('save') }}</button>
      </div>
    </div>
  `,
})
export class ManufacturerDialog {
  private readonly api = inject(ManufacturersApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject<MatDialogRef<ManufacturerDialog>>(MatDialogRef);

  readonly manufacturer = inject<Manufacturer | null>(MAT_DIALOG_DATA);
  readonly busy = signal(false);

  name = this.manufacturer?.name ?? '';

  async save(): Promise<void> {
    if (!this.name.trim()) return;
    this.busy.set(true);
    try {
      if (this.manufacturer) await lastValueFrom(this.api.update(this.manufacturer.id, this.name.trim()));
      else await lastValueFrom(this.api.create(this.name.trim()));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatButtonModule, MatDialogModule, TranslocoModule],
  template: `
    <div class="confirm" *transloco="let t">
      <div mat-dialog-content>
        <p>{{ t('delete_confirm') }}</p>
        <p class="target">{{ target }}</p>
      </div>
      <div mat-dialog-actions align="end">
        <button mat-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button class="danger" [mat-dialog-close]="true">{{ t('delete') }}</button>
      </div>
    </div>
  `,
  styles: `
    p { margin: 0; }
    .target { margin-top: 6px; font-weight: 700; }
    .danger { background: var(--cx-danger); color: #fff; }
  `,
})
export class ConfirmDialog {
  readonly target = inject<string>(MAT_DIALOG_DATA);
}
