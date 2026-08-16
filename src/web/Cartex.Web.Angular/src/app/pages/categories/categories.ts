import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CategoriesApi, Category } from '../../core/api/catalog.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

@Component({
  selector: 'app-categories',
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
  templateUrl: './categories.html',
  styleUrl: './categories.scss',
})
export class Categories implements OnInit {
  private readonly api = inject(CategoriesApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);

  private readonly auth = inject(AuthService);
  readonly canCreate = this.auth.hasPermission('categories.create');
  readonly canEdit = this.auth.hasPermission('categories.edit');
  readonly loading = signal(true);
  readonly all = signal<Category[]>([]);
  readonly search = signal('');
  readonly items = computed(() => {
    const q = this.search().toLowerCase();
    return q ? this.all().filter((c) => c.name.toLowerCase().includes(q)) : this.all();
  });
  readonly columns = ['name', 'parent', 'description'];

  ngOnInit(): void {
    void this.load();
  }

  openCreate(): void {
    if (!this.canCreate) return;
    this.openDialog(null);
  }

  openEdit(category: Category): void {
    if (this.canEdit) this.openDialog(category);
  }

  private openDialog(category: Category | null): void {
    this.dialog
      .open(CategoryDialog, {
        data: { category, all: this.all() },
        width: '440px',
        maxWidth: '94vw',
        autoFocus: false,
      })
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
  selector: 'app-category-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    TranslocoModule,
  ],
  styleUrl: './categories.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ data.category ? t('edit') : t('add') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-body">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('name') }}</mat-label>
          <input matInput [(ngModel)]="name" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('parent') }}</mat-label>
          <mat-select [(ngModel)]="parentId">
            <mat-option [value]="null">—</mat-option>
            @for (c of parents; track c.id) {
              <mat-option [value]="c.id">{{ c.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('description') }}</mat-label>
          <input matInput [(ngModel)]="description" />
        </mat-form-field>
      </div>
      <div mat-dialog-actions align="end">
        <button mat-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button [disabled]="busy() || !name.trim()" (click)="save()">{{ t('save') }}</button>
      </div>
    </div>
  `,
})
export class CategoryDialog {
  private readonly api = inject(CategoriesApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject<MatDialogRef<CategoryDialog>>(MatDialogRef);

  readonly data = inject<{ category: Category | null; all: Category[] }>(MAT_DIALOG_DATA);
  readonly busy = signal(false);
  readonly parents = this.data.all.filter((c) => c.id !== this.data.category?.id);

  name = this.data.category?.name ?? '';
  parentId = this.data.category?.parentId ?? null;
  description = this.data.category?.description ?? '';

  async save(): Promise<void> {
    if (!this.name.trim()) return;
    this.busy.set(true);
    try {
      const body = {
        name: this.name.trim(),
        parentId: this.parentId,
        description: this.description.trim() || null,
      };
      if (this.data.category) await lastValueFrom(this.api.update(this.data.category.id, body));
      else await lastValueFrom(this.api.create(body));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (e) {
      this.notify.error(e);
    } finally {
      this.busy.set(false);
    }
  }
}
