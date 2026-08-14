import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
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
  isTrusted: boolean;
  status: string;
  lastSeenAt: string | null;
  endpoints: PrinterEndpoint[];
  originalIsTrusted?: boolean;
}

interface PrintRequesterDevice {
  id: number;
  deviceId: string;
  name: string;
  client: string | null;
  isTrusted: boolean;
  lastSeenAt: string;
  lastUsername: string | null;
  originalIsTrusted?: boolean;
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
  targets: RouteTarget[];
}

interface EndpointChoice {
  endpoint: PrinterEndpoint;
  deviceName: string;
  selected: boolean;
  enabled: boolean;
  priority: number;
}

interface PrintJob {
  id: number;
  kind: string;
  status: string;
  summary: string | null;
  sourceType: string;
  sourceId: string;
  copies: number;
  requestedByName: string | null;
  requestedDeviceName: string | null;
  requestedClient: string | null;
  assignedDeviceName: string | null;
  assignedPrinterName: string | null;
  createdAt: string;
  errorMessage: string | null;
  errorCode: string | null;
}

@Component({
  selector: 'app-printing',
  imports: [DatePipe, FormsModule, MatButtonModule, MatCheckboxModule, MatIconModule, MatProgressBarModule, MatSlideToggleModule, TranslocoModule, PageHeader],
  template: `
    <ng-container *transloco="let t">
      <cx-page-header title="Tarmoq printerlari" subtitle="Chek, barkod, Z-hisobot va hujjatlarni turli qurilmalardagi printerlarga yo‘naltirish" />
      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      <div class="layout" [class.jobs-only]="!canViewRoutes">
        @if (canViewRoutes) {
        <section class="cx-card panel">
          <div class="head"><h3>Tarmoq qurilmalari</h3><mat-icon>devices</mat-icon></div>
          <div class="kind-tabs device-tabs">
            <button type="button" [class.active]="deviceTab === 'hosts'" (click)="deviceTab = 'hosts'">Printerlar</button>
            <button type="button" [class.active]="deviceTab === 'requesters'" (click)="deviceTab = 'requesters'">So‘rov qurilmalari</button>
          </div>
          @if (deviceTab === 'hosts') {
            <p class="muted device-note">Faol qurilmalardagi mos printerlargina yo‘naltirishda qatnashadi.</p>
            @for (node of nodes(); track node.id) {
              <article class="node">
                <div class="node-title"><strong>{{ node.name }}</strong><span>{{ node.status }}</span></div>
                <small>{{ node.deviceId }}</small>
                <mat-slide-toggle [(ngModel)]="node.isTrusted" [disabled]="!canManageNodes" title="O‘chirilsa bu kompyuterdagi printerlar yo‘naltirishda ishlatilmaydi">Ishonchli</mat-slide-toggle>
                @for (endpoint of node.endpoints; track endpoint.id) {
                  <div class="endpoint"><span>{{ endpoint.displayName }}</span><small>{{ endpoint.capabilities }} · {{ endpoint.status }}</small></div>
                }
              </article>
            }
            @if (!nodes().length && !loading()) { <p class="muted">Hali print-host ro‘yxatdan o‘tmagan.</p> }
            @if (canManageNodes) {
              <button matButton="filled" (click)="saveHosts()"><mat-icon>save</mat-icon>Printerlarni saqlash</button>
            }
          } @else {
            <p class="muted device-note">Faqat ruxsat berilgan telefon, web va desktop qurilmalardan print so‘rovi qabul qilinadi.</p>
            @for (device of requesterDevices(); track device.id) {
              <article class="node requester">
                <div class="node-title"><strong>{{ device.name }}</strong><mat-slide-toggle [(ngModel)]="device.isTrusted" [disabled]="!canManageNodes" title="Yoqilsa ushbu qurilmadan kelgan masofaviy print so‘rovlari qabul qilinadi">Printga ruxsat</mat-slide-toggle></div>
                <small>{{ device.lastUsername }} · {{ device.client }} · {{ device.lastSeenAt | date:'dd.MM.yyyy HH:mm' }}</small>
              </article>
            }
            @if (canManageNodes) {
              <button matButton="filled" (click)="saveRequesters()"><mat-icon>save</mat-icon>Ruxsatlarni saqlash</button>
            }
          }
        </section>
        }

        <section class="cx-card panel route">
          @if (canViewRoutes) {
          <div class="head"><h3>Print turi bo‘yicha yo‘naltirish</h3><mat-icon>alt_route</mat-icon></div>
          <div class="kind-tabs">
            @for (item of kinds; track item) {
              <button type="button" [class.active]="kind === item" (click)="selectKind(item)">{{ kindLabel(item) }}</button>
            }
          </div>
          <label>Yo‘naltirish rejimi<select [(ngModel)]="policy.routingMode" title="Lokal printer, qat’iy prioritet yoki faqat lokal ishlash tartibi">@for (item of routingModes; track item) { <option [value]="item">{{ item }}</option> }</select></label>
          <div class="switches">
            <mat-slide-toggle [(ngModel)]="policy.isEnabled">Bu print turi faol</mat-slide-toggle>
            <mat-slide-toggle [(ngModel)]="policy.allowFallback">Mos printerga avtomatik o‘tish</mat-slide-toggle>
          </div>
          <div class="fields two">
            <label title="Oxirgi muvaffaqiyatli printer qachongacha birinchi tanlanishini boshqaradi">Muvaffaqiyatli printer rejimi<select [(ngModel)]="policy.stickyMode">@for (item of stickyModes; track item) { <option [value]="item">{{ item }}</option> }</select></label>
            <label title="Duration rejimida muvaffaqiyatli printer qancha vaqt afzal bo‘lishi">Standart bo‘lib turish vaqti (daqiqa)<input type="number" min="0" max="43200" step="0.5" [(ngModel)]="stickyMinutes" /></label>
          </div>
          <div class="fields limits">
            <label>Maks. nusxa<input type="number" min="1" max="100" [(ngModel)]="policy.maxCopies" /></label>
            <label>Vazifa / daqiqa<input type="number" min="1" max="1000" [(ngModel)]="policy.maxJobsPerMinute" /></label>
            <label>Nusxa / daqiqa<input type="number" min="1" max="5000" [(ngModel)]="policy.maxCopiesPerMinute" /></label>
            <label>Kutish (soniya)<input type="number" min="5" max="300" [(ngModel)]="policy.assignmentTimeoutSeconds" /></label>
          </div>
          <h4 title="Tanlangan printerni tutqichidan ushlab kerakli joyga suring">Printerlar tartibi</h4>
          @for (choice of choices(); track choice.endpoint.id) {
            <div class="choice" [class.dragging]="draggedChoice === choice" (dragover)="$event.preventDefault()" (drop)="dropChoice(choice)">
              <span class="grip" draggable="true" (dragstart)="startChoiceDrag(choice, $event)" title="Prioritetni o‘zgartirish uchun torting">⠿</span>
              <mat-checkbox [(ngModel)]="choice.selected" />
              <div><strong>{{ choice.endpoint.displayName }}</strong><small>{{ choice.deviceName }} · {{ choice.endpoint.status }}</small></div>
              <mat-slide-toggle [(ngModel)]="choice.enabled" [disabled]="!choice.selected" />
            </div>
          }
          @if (!choices().length) { <p class="muted">Bu tur uchun mos printer topilmadi.</p> }
          @if (canEditRoutes) {
            <button matButton="filled" (click)="saveRoute()"><mat-icon>save</mat-icon>Yo‘naltirishni saqlash</button>
          }
          }
          @if (canViewJobs) {
            <div class="jobs-head"><div><h3>Oxirgi print vazifalari</h3><small>Yuborilgan ma’lumot, qurilma, printer va bajarilish holati</small></div><button matButton (click)="loadJobs()"><mat-icon>refresh</mat-icon>Yangilash</button></div>
            <div class="jobs">
              @for (job of jobs(); track job.id) {
                <article class="job">
                  <div class="job-head"><strong>{{ job.kind }}</strong><span>{{ job.status }}</span><small>×{{ job.copies }} · {{ job.createdAt | date:'dd.MM.yyyy HH:mm:ss' }}</small></div>
                  <p>{{ job.summary || job.sourceType + ' #' + job.sourceId }}</p>
                  <small>{{ job.requestedByName }} · {{ job.requestedDeviceName }} · {{ job.requestedClient }}</small>
                  <small>{{ job.assignedDeviceName }} · {{ job.assignedPrinterName }}</small>
                  @if (job.errorMessage || job.errorCode) { <small class="error">{{ job.errorMessage || job.errorCode }}</small> }
                  <div class="job-actions">
                    @if (canRetryJobs && (job.status === 'Failed' || job.status === 'Cancelled')) { <button matButton (click)="retryJob(job)">Qayta urinish</button> }
                    @if (canCancelJobs && (job.status === 'Pending' || job.status === 'Assigned')) { <button matButton (click)="cancelJob(job)">Bekor qilish</button> }
                  </div>
                </article>
              }
            </div>
          }
        </section>
      </div>
    </ng-container>
  `,
  styles: `
    :host { display: block; }
    .layout { display: grid; grid-template-columns: minmax(300px, 430px) minmax(500px, 1fr); gap: 16px; }
    .layout.jobs-only { grid-template-columns: minmax(0, 900px); }
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
    .choice div { flex: 1; display: flex; min-width: 0; flex-direction: column; }
    .grip { cursor: grab; color: var(--cx-text-3); font-size: 21px; user-select: none; } .choice.dragging { opacity: .55; }
    .kind-tabs { display: grid; grid-template-columns: repeat(4, 1fr); border-bottom: 1px solid var(--cx-border); }
    .device-tabs { grid-template-columns: repeat(2, 1fr); }
    .device-note { margin: 0; font-size: 12px; }
    .kind-tabs button { min-height: 42px; border: 0; border-bottom: 2px solid transparent; background: transparent; color: var(--cx-text-2); cursor: pointer; }
    .kind-tabs button:hover { background: var(--cx-surface-2); }
    .kind-tabs button.active { border-bottom-color: var(--cx-brand); color: var(--cx-brand); }
    .requesters-title { display: flex; flex-direction: column; gap: 2px; margin-top: 8px; padding-top: 12px; border-top: 1px solid var(--cx-border); }
    .jobs-head, .job-head, .job-actions { display: flex; align-items: center; gap: 8px; } .jobs-head { justify-content: space-between; border-top: 1px solid var(--cx-border); padding-top: 16px; }
    .jobs { display: grid; gap: 8px; max-height: 560px; overflow: auto; } .job { border: 1px solid var(--cx-border); border-radius: 9px; padding: 11px; display: grid; gap: 4px; }
    .job p { margin: 3px 0; } .job-head small { margin-left: auto; } .job-head span { border-radius: 12px; padding: 2px 8px; background: var(--cx-surface-2); font-size: 11px; }
    .job-actions { justify-content: flex-end; } .error { color: #dc2626; }
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
  readonly canViewRoutes = this.auth.hasPermission('printing.nodes.view') && this.auth.hasPermission('printing.routes.view');
  readonly canViewJobs = this.auth.hasPermission('printing.jobs.viewOwn|printing.jobs.viewBranch');
  readonly canCancelJobs = this.auth.hasPermission('printing.jobs.cancel');
  readonly canRetryJobs = this.auth.hasPermission('printing.jobs.retry');
  readonly loading = signal(true);
  readonly nodes = signal<PrintNode[]>([]);
  readonly requesterDevices = signal<PrintRequesterDevice[]>([]);
  readonly choices = signal<EndpointChoice[]>([]);
  readonly jobs = signal<PrintJob[]>([]);
  readonly kinds = ['Receipt', 'CartProforma', 'BarcodeLabel', 'ZReport', 'Document'];
  readonly routingModes = ['LocalFirst', 'PriorityOnly', 'LocalOnly'];
  readonly stickyModes = ['Disabled', 'Duration', 'UntilFailure', 'Permanent'];
  private policies: RoutingPolicy[] = [];
  private branchId = 0;
  kind = 'Receipt';
  deviceTab: 'hosts' | 'requesters' = 'hosts';
  stickyMinutes = 10;
  policy: RoutingPolicy = this.defaultPolicy();
  draggedChoice: EndpointChoice | null = null;

  async ngOnInit(): Promise<void> {
    try {
      const context = await lastValueFrom(this.http.get<{ defaultBranchId: number; branches: { id: number }[] }>('/api/auth/context'));
      this.branchId = context.defaultBranchId || context.branches[0]?.id || 0;
      if (!this.branchId) return;
      if (this.canViewRoutes) {
        const [nodes, requesterDevices, policies] = await Promise.all([
          lastValueFrom(this.http.get<PrintNode[]>('/api/printing/nodes', { params: { branchId: this.branchId } })),
          lastValueFrom(this.http.get<PrintRequesterDevice[]>('/api/printing/requesters', { params: { branchId: this.branchId } })),
          lastValueFrom(this.http.get<RoutingPolicy[]>('/api/printing/routes', { params: { branchId: this.branchId } })),
        ]);
        this.nodes.set(nodes.map((node) => ({ ...node, originalIsTrusted: node.isTrusted })));
        this.requesterDevices.set(requesterDevices.map((device) => ({ ...device, originalIsTrusted: device.isTrusted })));
        this.policies = policies;
        this.applyPolicy();
      }
      if (this.canViewJobs) await this.loadJobs();
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

  selectKind(kind: string): void { this.kind = kind; this.applyPolicy(); }
  kindLabel(kind: string): string { return ({ Receipt: 'Chek', CartProforma: 'Savat', BarcodeLabel: 'Etiketka', ZReport: 'Z-hisobot', Document: 'Hujjat' } as Record<string, string>)[kind] ?? kind; }

  startChoiceDrag(choice: EndpointChoice, event: DragEvent): void {
    if (!choice.selected) { event.preventDefault(); return; }
    this.draggedChoice = choice;
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  dropChoice(target: EndpointChoice): void {
    const source = this.draggedChoice;
    this.draggedChoice = null;
    if (!source || source === target || !source.selected || !target.selected) return;
    const choices = [...this.choices()];
    const from = choices.indexOf(source);
    const to = choices.indexOf(target);
    choices.splice(to, 0, choices.splice(from, 1)[0]);
    choices.filter((choice) => choice.selected).forEach((choice, index) => choice.priority = index + 1);
    this.choices.set(choices);
  }

  async loadJobs(): Promise<void> {
    try { this.jobs.set(await lastValueFrom(this.http.get<PrintJob[]>('/api/printing/jobs', { params: { branchId: this.branchId, take: 50 } }))); }
    catch (error) { this.notify.error(error); }
  }

  async cancelJob(job: PrintJob): Promise<void> {
    try { await lastValueFrom(this.http.post(`/api/printing/jobs/${job.id}/cancel`, {})); await this.loadJobs(); }
    catch (error) { this.notify.error(error); }
  }

  async retryJob(job: PrintJob): Promise<void> {
    try { await lastValueFrom(this.http.post(`/api/printing/jobs/${job.id}/retry`, {})); await this.loadJobs(); }
    catch (error) { this.notify.error(error); }
  }

  async saveHosts(): Promise<void> {
    try {
      for (const node of this.nodes().filter((item) => item.isTrusted !== item.originalIsTrusted)) {
        await lastValueFrom(this.http.put(`/api/printing/nodes/${node.id}`, { isTrusted: node.isTrusted }));
        node.originalIsTrusted = node.isTrusted;
      }
      this.notify.success(this.transloco.translate('success'));
    } catch (error) { this.notify.error(error); }
  }

  async saveRequesters(): Promise<void> {
    try {
      for (const device of this.requesterDevices().filter((item) => item.isTrusted !== item.originalIsTrusted)) {
        await lastValueFrom(this.http.put(`/api/printing/requesters/${device.id}`, { isTrusted: device.isTrusted }));
        device.originalIsTrusted = device.isTrusted;
      }
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
    return { kind: this.kind, isEnabled: true, routingMode: 'LocalFirst', allowFallback: true, stickyMode: 'Duration', stickyDurationSeconds: 600, maxCopies: 3, maxJobsPerMinute: 20, maxCopiesPerMinute: 30, assignmentTimeoutSeconds: 20, targets: [] };
  }
}
