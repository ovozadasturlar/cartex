import { BreakpointObserver } from '@angular/cdk/layout';
import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

/// Ekran o'lchamlari bitta joyda turadi: ilgari har sahifa o'z chegarasini tanlagani uchun
/// (600, 640, 699, 720, 900, 1000...) bir xil qurilmada bir bo'lim telefon, boshqasi planshet
/// ko'rinishida chiqib qolardi.
export const CX_PHONE_MAX = 767;
export const CX_TABLET_MAX = 1199;

@Injectable({ providedIn: 'root' })
export class LayoutService {
  private readonly breakpoints = inject(BreakpointObserver);

  readonly isPhone = toSignal(
    this.breakpoints.observe(`(max-width: ${CX_PHONE_MAX}px)`).pipe(map((r) => r.matches)),
    { initialValue: window.innerWidth <= CX_PHONE_MAX },
  );

  readonly isDesktop = toSignal(
    this.breakpoints.observe(`(min-width: ${CX_TABLET_MAX + 1}px)`).pipe(map((r) => r.matches)),
    { initialValue: window.innerWidth > CX_TABLET_MAX },
  );

  readonly isTablet = computed(() => !this.isPhone() && !this.isDesktop());

  /// Barmoq bilan boshqariladigan ekran: tugmalar kattaroq, jadval o'rniga karta.
  readonly isTouchLayout = computed(() => !this.isDesktop());

  /// Kassada katalog va savat yonma-yon faqat shu enidan boshlab sig'adi. Undan tor ekranda
  /// (telefon va tor planshet) ular almashib turadi — shuning uchun chegara alohida.
  readonly isSplitPos = toSignal(
    this.breakpoints.observe('(min-width: 1000px)').pipe(map((r) => r.matches)),
    { initialValue: window.innerWidth >= 1000 },
  );
}
