import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { lastValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { PageHeader } from '../../shared/page-header';

interface PrinterEndpoint {
  id: number;
  displayName: string;
  systemName: string;
  capabilities: string;
  status: string;
  isEnabled: boolean;
}

interface PrintNode {
  id: number;
  deviceId: string;
  name: string;
  isEnabled: boolean;
  isTrusted: boolean;
  status: string;
  lastSeenAt: string | null;
  endpoints: PrinterEndpoint[];
}

interface RouteTarget {
  endpointId: number;
  priority: number;
  isEnabled: boolean;
}

interface RoutingPolicy {
  kind: string;
  isEnabled: boolean;
  routingMode: string;
  allowFallback: boolean;
  stickyMode: string;
  stickyDurationSeconds: number;
  maxCopies: number;
  maxJobsPerMinute: number;
  maxCopiesPerMinute: number;
  assignmentTimeoutSeconds: number;
  requireTrustedNode: boolean;
  targets: RouteTarget[];
}

interface EndpointChoice {
  endpoint: PrinterEndpoint;
  deviceName: string;
  selected: boolean;
  enabled: boolean;
  priority: number;
}

@Component({
  selector: 'app-printing',
  imports: [FormsModule, MatButtonModule, MatCheckboxModule, MatIconModule, MatProgressBarModule, MatSlideToggleModule, TranslocoModule, PageHeader],
  template: `
    <ng-container *transloco="let t">
      <cx-page-header title="Tarmoq printerlari" subtitle="Chek, barkod, Z-hisobot va hujjatlarni turli qurilmalardagi printerlarga yo‘naltirish" />
      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      <div class="layout">
        <section class="cx-card panel">
          <div class="head"><h3>Print qurilmalari</h3><mat-icon>devices</mat-icon></div>
          @for (node of nodes(); track node.id) {
            <article class="node">
              <div class="node-title"><strong>{{ node.name }}</strong><span>{{ node.status }}</span></div>
              <small>{{ node.deviceId }}</small>
              <div class="toggles">
                <mat-slide-toggle [(ngModel)]="node.isEnabled" [disabled]="!canManageNodes">Faol</mat-slide-toggle>
                <mat-slide-toggle [(ngModel)]="node.isTrusted" [disabled]="!canManageNodes">Ishonchli</mat-slide-toggle>
              </div>
              @for (endpoint of node.endpoints; track endpoint.id) {
                <div class="endpoint"><span>{{ endpoint.displayName }}</span><small>{{ endpoint.capabilities }} · {{ endpoint.status }}</small></div>
              }
            </article>
          }
          @if (!nodes().length && !loading()) { <p class="muted">Hali print-host ro‘yxatdan o‘tmagan.</p> }
          @if (canManageNodes) {
            <button matButton="filled" (click)="saveNodes()"><mat-icon>save</mat-icon>Qurilmalarni saqlash</button>
          }
        </section>

        <section class="cx-card panel route">
          <div class="head"><h3>Print turi bo‘yicha yo‘naltirish</h3><mat-icon>alt_route</mat-icon></div>
          <div class="fields two">
            <label>Print turi<select [(ngModel)]="kind" (ngModelChange)="applyPolicy()">@for (item of kinds; track item) { <option [value]="item">{{ item }}</option> }</select></label>
            <label>Yo‘naltirish rejimi<select [(ngModel)]="policy.routingMode">@for (item of routingModes; track item) { <option [value]="item">{{ item }}</option> }</select></label>
          </div>
          <div class="switches">
            <mat-slide-toggle [(ngModel)]="policy.isEnabled">Bu print turi faol</mat-slide-toggle>
            <mat-slide-toggle [(ngModel)]="policy.allowFallback">Mos printerga avtomatik o‘tish</mat-slide-toggle>
            <mat-slide-toggle [(ngModel)]="policy.requireTrustedNode">Faqat ishonchli qurilmalar</mat-slide-toggle>
          </div>
          <div class="fields two">
            <label>Muvaffaqiyatli printer rejimi<select [(ngModel)]="policy.stickyMode">@for (item of stickyModes; track item) { <option [value]="item">{{ item }}</option> }</select></label>
            <label>Standart bo‘lib turish vaqti (daqiqa)<input type="number" min="0" max="43200" step="0.5" [(ngModel)]="stickyMinutes" /></label>
          </div>
          <div class="fields limits">
            <label>Maks. nusxa<input type="number" min="1" max="100" [(ngModel)]="policy.maxCopies" /></label>
            <label>Vazifa / daqiqa<input type="number" min="1" max="1000" [(ngModel)]="policy.maxJobsPerMinute" /></label>
            <label>Nusxa / daqiqa<input type="number" min="1" max="5000" [(ngModel)]="policy.maxCopiesPerMinute" /></label>
            <label>Kutish (soniya)<input type="number" min="5" max="300" [(ngModel)]="policy.assignmentTimeoutSeconds" /></label>
          </div>
          <h4>Printerlar prioriteti</h4>
          @for (choice of choices(); track choice.endpoint.id) {
            <div class="choice">
              <mat-checkbox [(ngModel)]="choice.selected" />
              <div><strong>{{ choice.endpoint.displayName }}</strong><small>{{ choice.deviceName }} · {{ choice.endpoint.status }}</small></div>
              <input aria-label="Priority" type="number" min="1" max="1000" [(ngModel)]="choice.priority" [disabled]="!choice.selected" />
              <mat-slide-toggle [(ngModel)]="choice.enabled" [disabled]="!choice.selected" />
            </div>
          }
          @if (!choices().length) { <p class="muted">Bu tur uchun mos printer topilmadi.</p> }
          @if (canEditRoutes) {
            <button matButton="filled" (click)="saveRoute()"><mat-icon>save</mat-icon>Yo‘naltirishni saqlash</button>
          }
        </section>
      </div>
    </ng-container>
  `,
  styles: `
    :host { display: block; }
    .layout { display: grid; grid-template-columns: minmax(300px, 430px) minmax(500px, 1fr); gap: 16px; }
    .panel { padding: 18px; display: flex; flex-direction: column; gap: 14px; min-width: 0; }
    .head, .node-title, .toggles, .choice { display: flex; align-items: center; gap: 12px; }
    .head { justify-content: space-between; } h3, h4 { margin: 0; } .node-title span { margin-left: auto; color: var(--cx-text-3); font-size: 12px; }
    .node { border: 1px solid var(--cx-border); border-radius: 10px; padding: 12px; display: flex; flex-direction: column; gap: 8px; }
    small, .muted { color: var(--cx-text-3); } .toggles { justify-content: space-between; flex-wrap: wrap; }
    .endpoint { border-top: 1px solid var(--cx-border); padding-top: 7px; display: flex; justify-content: space-between; gap: 10px; }
    .fields { display: grid; gap: 12px; } .two { grid-template-columns: 1fr 1fr; } .limits { grid-template-columns: repeat(4, 1fr); }
    label { display: flex; flex-direction: column; gap: 5px; color: var(--cx-text-2); font-size: 12px; }
    input, select { min-height: 40px; border: 1px solid var(--cx-border); border-radius: 8px; padding: 7px 10px; background: var(--cx-surface); color: var(--cx-text); }
    .switches { display: flex; gap: 18px; flex-wrap: wrap; }
    .choice { border: 1px solid var(--cx-border); border-radius: 9px; padding: 9px; }
    .choice div { flex: 1; display: flex; min-width: 0; flex-direction: column; } .choice input { width: 76px; }
    button { align-self: flex-end; }
    @media (max-width: 1050px) { .layout { grid-template-columns: 1fr; } }
    @media (max-width: 650px) { .two, .limits { grid-template-columns: 1fr; } .choice { flex-wrap: wrap; } .endpoint { flex-direction: column; } }
  `,
})
export class Printing implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotifyService);
  private readonly transloco = inject(TranslocoService);
  private readonly auth = inject(AuthService);
  readonly canManageNodes = this.auth.hasPermission('printing.nodes.manage');
  readonly canEditRoutes = this.auth.hasPermission('printing.routes.edit');
  readonly loading = signal(true);
  readonly nodes = signal<PrintNode[]>([]);
  readonly choices = signal<EndpointChoice[]>([]);
  readonly kinds = ['Receipt', 'BarcodeLabel', 'ZReport', 'Document'];
  readonly routingModes = ['LocalFirst', 'PriorityOnly', 'LocalOnly'];
  readonly stickyModes = ['Disabled', 'Duration', 'UntilFailure', 'Permanent'];
  private policies: RoutingPolicy[] = [];
  private branchId = 0;
  kind = 'Receipt';
  stickyMinutes = 10;
  policy: RoutingPolicy = this.defaultPolicy();

  async ngOnInit(): Promise<void> {
    try {
      const context = await lastValueFrom(this.http.get<{ defaultBranchId: number; branches: { id: number }[] }>('/api/auth/context'));
      this.branchId = context.defaultBranchId || context.branches[0]?.id || 0;
      if (!this.branchId) return;
      const [nodes, policies] = await Promise.all([
        lastValueFrom(this.http.get<PrintNode[]>('/api/printing/nodes', { params: { branchId: this.branchId } })),
        lastValueFrom(this.http.get<RoutingPolicy[]>('/api/printing/routes', { params: { branchId: this.branchId } })),
      ]);
      this.nodes.set(nodes);
      this.policies = policies;
      this.applyPolicy();
    } catch (error) {
      this.notify.error(error);
    } finally {
      this.loading.set(false);
    }
  }

  applyPolicy(): void {
    this.policy = structuredClone(this.policies.find((item) => item.kind === this.kind) ?? this.defaultPolicy());
    this.stickyMinutes = this.policy.stickyDurationSeconds / 60;
    const existing = new Map(this.policy.targets.map((target) => [target.endpointId, target]));
    this.choices.set(this.nodes().flatMap((node) => node.endpoints
      .filter((endpoint) => endpoint.capabilities.includes(this.kind))
      .map((endpoint) => {
        const target = existing.get(endpoint.id);
        return { endpoint, deviceName: node.name, selected: !!target, enabled: target?.isEnabled ?? true, priority: target?.priority ?? 1000 };
      })).sort((a, b) => Number(b.selected) - Number(a.selected) || a.priority - b.priority));
  }

  async saveNodes(): Promise<void> {
    try {
      for (const node of this.nodes())
        await lastValueFrom(this.http.put(`/api/printing/nodes/${node.id}`, { isEnabled: node.isEnabled, isTrusted: node.isTrusted }));
      this.notify.success(this.transloco.translate('success'));
    } catch (error) { this.notify.error(error); }
  }

  async saveRoute(): Promise<void> {
    const targets = this.choices().filter((choice) => choice.selected).sort((a, b) => a.priority - b.priority)
      .map((choice, index) => ({ endpointId: choice.endpoint.id, priority: index + 1, isEnabled: choice.enabled }));
    const body = { ...this.policy, stickyDurationSeconds: Math.round(this.stickyMinutes * 60), targets };
    try {
      const updated = await lastValueFrom(this.http.put<RoutingPolicy>(`/api/printing/routes/${this.kind}`, body, { params: { branchId: this.branchId } }));
      const index = this.policies.findIndex((item) => item.kind === updated.kind);
      if (index >= 0) this.policies[index] = updated; else this.policies.push(updated);
      this.applyPolicy();
      this.notify.success(this.transloco.translate('success'));
    } catch (error) { this.notify.error(error); }
  }

  private defaultPolicy(): RoutingPolicy {
    return { kind: this.kind, isEnabled: true, routingMode: 'LocalFirst', allowFallback: true, stickyMode: 'Duration', stickyDurationSeconds: 600, maxCopies: 3, maxJobsPerMinute: 20, maxCopiesPerMinute: 30, assignmentTimeoutSeconds: 20, requireTrustedNode: true, targets: [] };
  }
}

