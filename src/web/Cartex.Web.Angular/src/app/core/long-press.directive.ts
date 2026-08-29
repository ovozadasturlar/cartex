import { Directive, ElementRef, OnDestroy, inject, output } from '@angular/core';

/// Desktopdagi `PressGestures` ning veb variantini: plitkani bosib turish kartochkani
/// ochadi, oddiy bosish esa savatga qo'shadi. Sichqonchada o'ng tugma ham shu ishni qiladi.
@Directive({
  selector: '[cxLongPress]',
})
export class LongPressDirective implements OnDestroy {
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly cxLongPress = output<void>();

  private timer?: ReturnType<typeof setTimeout>;
  private fired = false;

  constructor() {
    const el = this.host.nativeElement as HTMLElement;
    el.addEventListener('pointerdown', this.down);
    el.addEventListener('pointerup', this.up);
    el.addEventListener('pointerleave', this.cancel);
    el.addEventListener('pointercancel', this.cancel);
    el.addEventListener('contextmenu', this.menu);
    el.addEventListener('click', this.click, true);
  }

  ngOnDestroy(): void {
    const el = this.host.nativeElement as HTMLElement;
    el.removeEventListener('pointerdown', this.down);
    el.removeEventListener('pointerup', this.up);
    el.removeEventListener('pointerleave', this.cancel);
    el.removeEventListener('pointercancel', this.cancel);
    el.removeEventListener('contextmenu', this.menu);
    el.removeEventListener('click', this.click, true);
    clearTimeout(this.timer);
  }

  private readonly down = (): void => {
    this.fired = false;
    this.timer = setTimeout(() => {
      this.fired = true;
      this.cxLongPress.emit();
    }, 500);
  };

  private readonly up = (): void => clearTimeout(this.timer);
  private readonly cancel = (): void => {
    clearTimeout(this.timer);
    this.fired = false;
  };

  private readonly menu = (e: Event): void => {
    e.preventDefault();
    this.cancel();
    this.cxLongPress.emit();
  };

  /// Uzoq bosish ishlagach, qo'yib yuborishdagi oddiy bosish o'tib ketmasligi kerak —
  /// aks holda kartochka ochiladi va yon tomonda savatga ham qo'shilib qoladi.
  private readonly click = (e: Event): void => {
    if (!this.fired) return;
    e.stopPropagation();
    e.preventDefault();
    this.fired = false;
  };
}
