import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDragMove, CdkDragPreview, CdkDropList } from '@angular/cdk/drag-drop';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { CategoriesApi, Category } from '../../core/api/catalog.api';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { EmptyState } from '../../shared/empty-state';
import { PageHeader } from '../../shared/page-header';

type DropPosition = 'before' | 'after' | 'child';
type DropState = { targetId: number; position: DropPosition; valid: boolean };
type MovePlan = { parentId: number | null; sortOrder: number };

@Component({
  selector: 'app-categories',
  imports: [
    CdkDrag,
    CdkDragHandle,
    CdkDragPreview,
    CdkDropList,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    TranslocoModule,
    EmptyState,
    PageHeader,
  ],
  templateUrl: './categories.html',
  styleUrl: './categories.scss',
})
export class Categories implements OnInit, OnDestroy {
  private readonly api = inject(CategoriesApi);
  private readonly notify = inject(NotifyService);
  private readonly dialog = inject(MatDialog);
  private readonly auth = inject(AuthService);
  private searchTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly storageKey = `cartex.category-tree.expanded.${this.auth.currentUser()?.userId ?? 0}`;

  @ViewChild('treeScroll') private treeScroll?: ElementRef<HTMLElement>;

  readonly canCreate = this.auth.hasPermission('categories.create');
  readonly canEdit = this.auth.hasPermission('categories.edit');
  readonly loading = signal(true);
  readonly all = signal<Category[]>([]);
  readonly catalog = signal<Category[]>([]);
  readonly query = signal('');
  readonly expanded = signal(new Set(this.readExpanded()));
  readonly dropState = signal<DropState | null>(null);
  readonly visible = computed(() => {
    const result: Category[] = [];
    const query = this.query();
    const expanded = this.expanded();
    const add = (parentId: number | null): void => {
      for (const item of this.siblings(parentId)) {
        result.push(item);
        if (query || expanded.has(item.id)) add(item.id);
      }
    };
    add(null);
    return result;
  });
  readonly allExpanded = computed(() => {
    const expandable = this.all().filter((item) => this.hasChildren(item));
    return expandable.length > 0 && expandable.every((item) => this.expanded().has(item.id));
  });

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
  }

  hasChildren(category: Category): boolean {
    return this.all().some((item) => item.parentId === category.id);
  }

  isExpanded(category: Category): boolean {
    return !!this.query() || this.expanded().has(category.id);
  }

  toggle(category: Category, event: Event): void {
    event.stopPropagation();
    if (!this.hasChildren(category)) return;
    const next = new Set(this.expanded());
    if (next.has(category.id)) next.delete(category.id);
    else next.add(category.id);
    this.setExpanded(next);
  }

  toggleAll(): void {
    const next = this.allExpanded()
      ? new Set<number>()
      : new Set(this.all().filter((item) => this.hasChildren(item)).map((item) => item.id));
    this.setExpanded(next);
  }

  search(value: string): void {
    this.query.set(value.trim());
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => void this.load(this.query()), 220);
  }

  openCreate(): void {
    if (this.canCreate) this.openDialog(null);
  }

  openEdit(category: Category, event?: Event): void {
    event?.stopPropagation();
    if (this.canEdit) this.openDialog(category);
  }

  openMerge(category: Category, event: Event): void {
    event.stopPropagation();
    if (!this.canEdit) return;
    const targets = this.catalog().filter((target) => this.canMerge(category, target));
    this.dialog
      .open(CategoryMergeDialog, {
        data: { source: category, targets },
        width: '500px',
        maxWidth: '94vw',
        autoFocus: false,
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) void this.load(this.query());
      });
  }

  dragMoved(event: CdkDragMove<Category>): void {
    const element = document.elementFromPoint(event.pointerPosition.x, event.pointerPosition.y)?.closest<HTMLElement>('.tree-row');
    const targetId = Number(element?.dataset['id']);
    const source = event.source.data;
    const target = this.all().find((item) => item.id === targetId);
    if (!element || !target || source.id === target.id) {
      this.dropState.set(null);
      return;
    }
    const bounds = element.getBoundingClientRect();
    const ratio = (event.pointerPosition.y - bounds.top) / Math.max(1, bounds.height);
    const position: DropPosition = ratio < 0.25 ? 'before' : ratio > 0.75 ? 'after' : 'child';
    this.dropState.set({ targetId, position, valid: this.plan(source, target, position) !== null });
    const scroller = this.treeScroll?.nativeElement;
    if (scroller) {
      const scrollBounds = scroller.getBoundingClientRect();
      if (event.pointerPosition.y < scrollBounds.top + 38) scroller.scrollTop -= 24;
      else if (event.pointerPosition.y > scrollBounds.bottom - 38) scroller.scrollTop += 24;
    }
  }

  async dropped(event: CdkDragDrop<Category[]>): Promise<void> {
    const source = event.item.data as Category;
    const state = this.dropState();
    this.dropState.set(null);
    if (!this.canEdit || !state?.valid) return;
    const target = this.all().find((item) => item.id === state.targetId);
    if (!target) return;
    const plan = this.plan(source, target, state.position);
    if (plan) await this.move(source, plan);
  }

  async keyMove(event: KeyboardEvent, source: Category): Promise<void> {
    if (!event.altKey || !this.canEdit || !!this.query()) return;
    let plan: MovePlan | null = null;
    if (event.key === 'ArrowUp' || event.key === 'ArrowDown') {
      const siblings = this.siblings(source.parentId);
      const index = siblings.findIndex((item) => item.id === source.id);
      const target = index + (event.key === 'ArrowUp' ? -1 : 1);
      if (index >= 0 && target >= 0 && target < siblings.length)
        plan = { parentId: source.parentId, sortOrder: target };
    } else if (event.key === 'ArrowLeft' && source.parentId !== null) {
      const parent = this.find(source.parentId);
      if (parent) {
        const siblings = this.siblings(parent.parentId).filter((item) => item.id !== source.id);
        plan = { parentId: parent.parentId, sortOrder: siblings.findIndex((item) => item.id === parent.id) + 1 };
      }
    } else if (event.key === 'ArrowRight') {
      const siblings = this.siblings(source.parentId);
      const index = siblings.findIndex((item) => item.id === source.id);
      if (index > 0) plan = { parentId: siblings[index - 1].id, sortOrder: this.siblings(siblings[index - 1].id).length };
    }
    if (!plan || !this.valid(source, plan)) return;
    event.preventDefault();
    await this.move(source, plan);
  }

  dropClass(category: Category, position: DropPosition): boolean {
    const state = this.dropState();
    return !!state?.valid && state.targetId === category.id && state.position === position;
  }

  invalidDrop(category: Category): boolean {
    const state = this.dropState();
    return !!state && !state.valid && state.targetId === category.id;
  }

  private openDialog(category: Category | null): void {
    this.dialog
      .open(CategoryDialog, {
        data: { category, all: this.catalog() },
        width: '480px',
        maxWidth: '94vw',
        autoFocus: false,
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) void this.load(this.query());
      });
  }

  private async load(search?: string): Promise<void> {
    this.loading.set(true);
    try {
      const categories = await lastValueFrom(this.api.all(search || undefined));
      this.all.set(categories);
      if (!search) this.catalog.set(categories);
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.loading.set(false);
    }
  }

  private async move(source: Category, plan: MovePlan): Promise<void> {
    try {
      await lastValueFrom(this.api.move(source.id, plan));
      const next = new Set(this.expanded());
      next.add(source.id);
      this.setExpanded(next);
      await this.load(this.query());
    } catch (error) {
      this.notify.error(error);
    }
  }

  private plan(source: Category, target: Category, position: DropPosition): MovePlan | null {
    if (source.id === target.id) return null;
    const plan = position === 'child'
      ? { parentId: target.id, sortOrder: this.siblings(target.id).length }
      : this.siblingPlan(source, target, position === 'after');
    return plan && this.valid(source, plan) ? plan : null;
  }

  private siblingPlan(source: Category, target: Category, after: boolean): MovePlan | null {
    const siblings = this.siblings(target.parentId).filter((item) => item.id !== source.id);
    const index = siblings.findIndex((item) => item.id === target.id);
    return index < 0 ? null : { parentId: target.parentId, sortOrder: index + (after ? 1 : 0) };
  }

  private valid(source: Category, plan: MovePlan): boolean {
    if (plan.parentId === source.id || this.isDescendant(source.id, plan.parentId)) return false;
    const parent = this.catalogFind(plan.parentId);
    const depth = parent ? parent.depth + 1 : 1;
    return depth + this.height(source.id) - 1 <= 3;
  }

  private canMerge(source: Category, target: Category): boolean {
    return source.id !== target.id
      && !this.isDescendant(source.id, target.id)
      && this.catalogSiblings(source.id).every((child) => target.depth + this.height(child.id) <= 3);
  }

  private isDescendant(sourceId: number, candidateId: number | null): boolean {
    let cursor = candidateId;
    while (cursor !== null) {
      if (cursor === sourceId) return true;
      cursor = this.catalogFind(cursor)?.parentId ?? null;
    }
    return false;
  }

  private height(id: number): number {
    const children = this.catalogSiblings(id);
    return children.length ? 1 + Math.max(...children.map((child) => this.height(child.id))) : 1;
  }

  private siblings(parentId: number | null): Category[] {
    return this.all()
      .filter((item) => item.parentId === parentId)
      .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name) || a.id - b.id);
  }

  private find(id: number | null): Category | undefined {
    return id === null ? undefined : this.all().find((item) => item.id === id);
  }

  private catalogSiblings(parentId: number | null): Category[] {
    return this.catalog()
      .filter((item) => item.parentId === parentId)
      .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name) || a.id - b.id);
  }

  private catalogFind(id: number | null): Category | undefined {
    return id === null ? undefined : this.catalog().find((item) => item.id === id);
  }

  private setExpanded(value: Set<number>): void {
    this.expanded.set(value);
    localStorage.setItem(this.storageKey, JSON.stringify([...value]));
  }

  private readExpanded(): number[] {
    try {
      const value = JSON.parse(localStorage.getItem(this.storageKey) ?? '[]') as unknown;
      return Array.isArray(value) ? value.filter((id): id is number => typeof id === 'number') : [];
    } catch {
      return [];
    }
  }
}

@Component({
  selector: 'app-category-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSelectModule, TranslocoModule],
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
            @for (category of parents; track category.id) {
              <mat-option [value]="category.id">{{ category.fullPath || category.name }}</mat-option>
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
  readonly parents = this.data.all.filter((candidate) => this.canParent(candidate));

  name = this.data.category?.name ?? '';
  parentId = this.data.category?.parentId ?? null;
  description = this.data.category?.description ?? '';

  async save(): Promise<void> {
    if (!this.name.trim()) return;
    this.busy.set(true);
    try {
      const body = { name: this.name.trim(), parentId: this.parentId, description: this.description.trim() || null };
      if (this.data.category) await lastValueFrom(this.api.update(this.data.category.id, body));
      else await lastValueFrom(this.api.create(body));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.busy.set(false);
    }
  }

  private canParent(candidate: Category): boolean {
    const source = this.data.category;
    if (!source) return candidate.depth < 3;
    if (candidate.id === source.id || this.isDescendant(source.id, candidate.id)) return false;
    return candidate.depth + this.height(source.id) <= 3;
  }

  private isDescendant(sourceId: number, candidateId: number): boolean {
    let cursor: number | null = candidateId;
    while (cursor !== null) {
      if (cursor === sourceId) return true;
      cursor = this.data.all.find((item) => item.id === cursor)?.parentId ?? null;
    }
    return false;
  }

  private height(id: number): number {
    const children = this.data.all.filter((item) => item.parentId === id);
    return children.length ? 1 + Math.max(...children.map((child) => this.height(child.id))) : 1;
  }
}

@Component({
  selector: 'app-category-merge-dialog',
  imports: [FormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatIconModule, MatSelectModule, TranslocoModule],
  styleUrl: './categories.scss',
  template: `
    <div class="dlg" *transloco="let t">
      <div class="dlg-head">
        <h2>{{ t('category_merge') }}</h2>
        <button mat-icon-button mat-dialog-close><mat-icon>close</mat-icon></button>
      </div>
      <div mat-dialog-content class="dlg-body">
        <p>{{ t('category_merge_confirm', { count: data.source.productCount, source: data.source.fullPath || data.source.name, target: targetPath }) }}</p>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ t('category_merge_target') }}</mat-label>
          <mat-select [(ngModel)]="targetId">
            @for (target of data.targets; track target.id) {
              <mat-option [value]="target.id">{{ target.fullPath || target.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>
      <div mat-dialog-actions align="end">
        <button mat-button mat-dialog-close>{{ t('cancel') }}</button>
        <button mat-flat-button [disabled]="busy() || targetId === null" (click)="merge()">{{ t('merge') }}</button>
      </div>
    </div>
  `,
})
export class CategoryMergeDialog {
  private readonly api = inject(CategoriesApi);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly ref = inject<MatDialogRef<CategoryMergeDialog>>(MatDialogRef);

  readonly data = inject<{ source: Category; targets: Category[] }>(MAT_DIALOG_DATA);
  readonly busy = signal(false);
  targetId: number | null = this.data.targets[0]?.id ?? null;

  get targetPath(): string {
    const target = this.data.targets.find((item) => item.id === this.targetId);
    return target?.fullPath || target?.name || '';
  }

  async merge(): Promise<void> {
    if (this.targetId === null) return;
    this.busy.set(true);
    try {
      await lastValueFrom(this.api.merge(this.data.source.id, this.targetId));
      this.notify.success(this.transloco.translate('success'));
      this.ref.close(true);
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.busy.set(false);
    }
  }
}
