import { HttpResponse } from '@angular/common/http';

export interface PagingMeta {
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface Paged<T> {
  items: T[];
  meta: PagingMeta;
}

export function toPaged<T>(resp: HttpResponse<T[]>): Paged<T> {
  const items = resp.body ?? [];
  const raw = resp.headers.get('X-Paging');
  // The header is PascalCase because it comes from the server's own metadata writer; naming the
  // shape here keeps `any` from leaking out of JSON.parse into every list page.
  interface RawPaging {
    TotalCount: number;
    Page: number;
    PageSize: number;
    TotalPages: number;
  }
  const meta: PagingMeta = raw
    ? (() => {
        const m = JSON.parse(raw) as RawPaging;
        return { totalCount: m.TotalCount, page: m.Page, pageSize: m.PageSize, totalPages: m.TotalPages };
      })()
    : { totalCount: items.length, page: 1, pageSize: items.length, totalPages: 1 };
  return { items, meta };
}

export interface ListQuery {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  descending?: boolean;
  search?: string;
  [extra: string]: string | number | boolean | undefined;
}

export function listParams(q: ListQuery): Record<string, string> {
  const p: Record<string, string> = {};
  for (const [k, v] of Object.entries(q))
    if (v !== undefined && v !== null && v !== '') p[k[0].toUpperCase() + k.slice(1)] = String(v);
  p['TimeZone'] = String(-new Date().getTimezoneOffset() / 60);
  return p;
}
