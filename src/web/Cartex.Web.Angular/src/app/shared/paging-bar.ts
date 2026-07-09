import { Component, input, output } from '@angular/core';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { PagingMeta } from '../core/paging';

@Component({
  selector: 'cx-paging-bar',
  imports: [MatPaginatorModule],
  template: `
    <mat-paginator
      [length]="meta().totalCount"
      [pageIndex]="meta().page - 1"
      [pageSize]="meta().pageSize"
      [pageSizeOptions]="[20, 30, 50, 80]"
      [showFirstLastButtons]="true"
      (page)="onPage($event)" />
  `,
})
export class PagingBar {
  readonly meta = input.required<PagingMeta>();
  readonly changed = output<{ page: number; pageSize: number }>();

  onPage(e: PageEvent): void {
    this.changed.emit({ page: e.pageIndex + 1, pageSize: e.pageSize });
  }
}
