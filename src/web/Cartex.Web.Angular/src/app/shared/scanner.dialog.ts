import { Component, ElementRef, OnDestroy, inject, signal, viewChild } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoModule } from '@jsverse/transloco';
import { BarcodeScannerService } from '../core/barcode-scanner.service';

export interface ScanFeedback {
  ok: boolean;
  message: string;
}

export interface ScannerData {
  /// Har o'qilgan kod shu yerga beriladi va javob ekranda ko'rsatiladi. Kamera yopilmaydi:
  /// kassir savatga ketma-ket bir necha mahsulot qo'sha olishi kerak.
  handle: (code: string) => Promise<ScanFeedback>;
}

/// Bir xil kod kamera oldida turganda sekundiga o'nlab marta o'qiladi; shu oraliqda takror
/// hisobga olinmaydi, aks holda bitta mahsulot savatga bir necha marta tushardi.
const REPEAT_GUARD_MS = 1500;

@Component({
  selector: 'cx-scanner-dialog',
  imports: [MatDialogModule, MatIconModule, TranslocoModule],
  template: `
    <div class="scanner" *transloco="let t">
      @if (error(); as message) {
        <div class="state">
          <mat-icon>videocam_off</mat-icon>
          <p>{{ t(message) }}</p>
          <button type="button" class="close-btn" (click)="ref.close()">{{ t('close') }}</button>
        </div>
      } @else {
        <video #preview playsinline muted></video>
        <div class="frame"></div>
        <div class="top">
          <span class="count">{{ t('scanned_count_fmt', { count: added() }) }}</span>
          <span class="spacer"></span>
          @if (torchAvailable()) {
            <button type="button" class="icon-btn" [class.on]="torchOn()" (click)="toggleTorch()">
              <mat-icon>{{ torchOn() ? 'flashlight_on' : 'flashlight_off' }}</mat-icon>
            </button>
          }
          <button type="button" class="icon-btn" (click)="ref.close()"><mat-icon>close</mat-icon></button>
        </div>
        <div class="bottom">
          @if (feedback(); as f) {
            <div class="feedback" [class.bad]="!f.ok">
              <mat-icon>{{ f.ok ? 'check_circle' : 'error' }}</mat-icon>
              <span>{{ f.message }}</span>
            </div>
          } @else {
            <div class="hint">{{ t('scanner_hint') }}</div>
          }
        </div>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    .scanner { position: relative; width: 100%; height: 100%; background: #000; overflow: hidden; }
    video { width: 100%; height: 100%; object-fit: cover; display: block; }
    .frame {
      position: absolute; left: 50%; top: 50%; transform: translate(-50%, -50%);
      width: min(78vw, 420px); height: min(40vw, 210px); pointer-events: none;
      border: 2px solid rgba(255, 255, 255, 0.9); border-radius: 14px;
      box-shadow: 0 0 0 100vmax rgba(0, 0, 0, 0.45);
    }
    .top, .bottom { position: absolute; left: 0; right: 0; display: flex; align-items: center; gap: 10px; padding: 12px 14px; }
    .top { top: 0; }
    .bottom { bottom: 0; justify-content: center; }
    .spacer { flex: 1; }
    .count { color: #fff; font-size: 13px; background: rgba(0, 0, 0, 0.45); padding: 5px 10px; border-radius: 999px; }
    .icon-btn {
      display: grid; place-items: center; width: 42px; height: 42px; border: 0; cursor: pointer;
      border-radius: 50%; background: rgba(0, 0, 0, 0.45); color: #fff;
    }
    .icon-btn.on { background: #fbbf24; color: #111; }
    .hint, .feedback {
      color: #fff; font-size: 14px; text-align: center; padding: 9px 14px; border-radius: 12px;
      background: rgba(0, 0, 0, 0.55); display: flex; align-items: center; gap: 8px; max-width: 92%;
    }
    .feedback { background: rgba(22, 163, 74, 0.92); }
    .feedback.bad { background: rgba(220, 38, 38, 0.92); }
    .state {
      display: grid; place-items: center; gap: 14px; height: 100%; padding: 28px; text-align: center;
      color: #fff;
    }
    .state mat-icon { font-size: 46px; width: 46px; height: 46px; opacity: 0.75; }
    .close-btn {
      border: 0; border-radius: 10px; padding: 10px 22px; cursor: pointer;
      background: #fff; color: #111; font-size: 14px;
    }
  `,
})
export class ScannerDialog implements OnDestroy {
  private readonly scanner = inject(BarcodeScannerService);
  private readonly data = inject<ScannerData>(MAT_DIALOG_DATA);
  readonly ref = inject(MatDialogRef<ScannerDialog>);
  private readonly preview = viewChild<ElementRef<HTMLVideoElement>>('preview');

  readonly error = signal<string | null>(null);
  readonly added = signal(0);
  readonly feedback = signal<ScanFeedback | null>(null);
  readonly torchOn = signal(false);
  readonly torchAvailable = signal(false);

  private stream: MediaStream | null = null;
  private stopped = false;
  private busy = false;
  private lastCode = '';
  private lastAt = 0;
  private timer?: ReturnType<typeof setInterval>;
  private feedbackTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    const blocked = this.scanner.blockedBy();
    if (blocked !== 'none') {
      this.error.set(blocked === 'insecure' ? 'scanner_insecure' : 'scanner_unsupported');
      return;
    }
    void this.start();
  }

  ngOnDestroy(): void {
    this.stopped = true;
    clearInterval(this.timer);
    clearTimeout(this.feedbackTimer);
    this.stream?.getTracks().forEach((track) => track.stop());
  }

  async toggleTorch(): Promise<void> {
    const track = this.stream?.getVideoTracks()[0];
    if (!track) return;
    const next = !this.torchOn();
    try {
      // `torch` hali standart TS tiplarida yo'q, lekin Android Chrome uni qo'llab-quvvatlaydi.
      await track.applyConstraints({ advanced: [{ torch: next }] } as unknown as MediaTrackConstraints);
      this.torchOn.set(next);
    } catch {
      this.torchAvailable.set(false);
    }
  }

  private async start(): Promise<void> {
    const detector = this.scanner.create();
    if (!detector) return;
    try {
      // Orqa kamera va baland ruxsat so'raladi: mayda shtrixkod past ruxsatda o'qilmaydi.
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: { ideal: 'environment' },
          width: { ideal: 1920 },
          height: { ideal: 1080 },
        },
      });
    } catch {
      this.error.set('scanner_denied');
      return;
    }

    const video = this.preview()?.nativeElement;
    if (!video) return;
    video.srcObject = this.stream;
    await video.play().catch(() => undefined);
    this.torchAvailable.set('torch' in (this.stream.getVideoTracks()[0]?.getCapabilities?.() ?? {}));

    // Har kadrda o'qish — qo'lda ushlab turilgan telefonda kod bir necha kadrgagina tushadi,
    // shuning uchun kadrni o'tkazib yubormaslik muhim. Qo'llab-quvvatlanmasa taymerga tushiladi.
    const withFrames = 'requestVideoFrameCallback' in video;
    const scanFrame = async (): Promise<void> => {
      if (this.stopped) return;
      await this.read(detector, video);
      if (withFrames && !this.stopped) {
        (video as unknown as { requestVideoFrameCallback: (cb: () => void) => void })
          .requestVideoFrameCallback(() => void scanFrame());
      }
    };
    if (withFrames) void scanFrame();
    else this.timer = setInterval(() => void this.read(detector, video), 120);
  }

  private async read(detector: { detect: (s: CanvasImageSource) => Promise<{ rawValue: string }[]> }, video: HTMLVideoElement): Promise<void> {
    if (this.busy || video.readyState < 2) return;
    this.busy = true;
    try {
      const [hit] = await detector.detect(video);
      const code = hit?.rawValue?.trim();
      if (code) await this.accept(code);
    } catch {
      // Bitta kadr o'qilmasa keyingisi o'qiladi — bu xato emas, oddiy hol.
    } finally {
      this.busy = false;
    }
  }

  private async accept(code: string): Promise<void> {
    const now = Date.now();
    if (code === this.lastCode && now - this.lastAt < REPEAT_GUARD_MS) return;
    this.lastCode = code;
    this.lastAt = now;
    navigator.vibrate?.(35);
    this.beep();
    const result = await this.data.handle(code);
    if (result.ok) this.added.update((count) => count + 1);
    this.show(result);
  }

  private show(result: ScanFeedback): void {
    this.feedback.set(result);
    clearTimeout(this.feedbackTimer);
    this.feedbackTimer = setTimeout(() => this.feedback.set(null), 2200);
  }

  private beep(): void {
    try {
      const context = new AudioContext();
      const gain = context.createGain();
      gain.gain.setValueAtTime(0.06, context.currentTime);
      gain.connect(context.destination);
      const oscillator = context.createOscillator();
      oscillator.type = 'square';
      oscillator.frequency.setValueAtTime(1180, context.currentTime);
      oscillator.connect(gain);
      oscillator.start();
      oscillator.stop(context.currentTime + 0.06);
      oscillator.onended = () => void context.close();
    } catch {
      // Ovoz chiqmasa skanerlash to'xtamaydi.
    }
  }
}
