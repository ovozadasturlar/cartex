import { Directive, ElementRef, HostListener, forwardRef, inject } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

const SEP = ' ';

function parseMoney(text: string): number {
  const v = Number(text.replaceAll(SEP, '').replace(',', '.'));
  return Number.isFinite(v) && v > 0 ? v : 0;
}

@Directive({
  selector: 'input[cxMoneyInput]',
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => MoneyInputDirective), multi: true }],
})
export class MoneyInputDirective implements ControlValueAccessor {
  private readonly input = inject<ElementRef<HTMLInputElement>>(ElementRef).nativeElement;
  private onChange: (value: number) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: number | null): void {
    this.input.value = value == null ? '' : this.group(String(value).replace('.', ','));
  }

  registerOnChange(fn: (value: number) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.input.disabled = disabled;
  }

  @HostListener('focus')
  onFocus(): void {
    this.input.select();
  }

  @HostListener('blur')
  onBlur(): void {
    this.onTouched();
  }

  @HostListener('input')
  onInput(): void {
    const raw = this.input.value;
    const caret = this.input.selectionStart ?? raw.length;
    const before = raw.slice(0, caret).replace(/[^\d.,]/g, '').length;
    const formatted = this.group(raw);
    this.input.value = formatted;
    let pos = 0;
    for (let seen = 0; pos < formatted.length && seen < before; pos++) {
      if (formatted[pos] !== SEP) seen++;
    }
    this.input.setSelectionRange(pos, pos);
    this.onChange(parseMoney(formatted));
  }

  private group(text: string): string {
    const cleaned = text.replace(/[^\d.,]/g, '');
    const sep = cleaned.search(/[.,]/);
    const int = (sep < 0 ? cleaned : cleaned.slice(0, sep)).replace(/^0+(?=\d)/, '');
    const grouped = int.replace(/\B(?=(\d{3})+(?!\d))/g, SEP);
    return sep < 0 ? grouped : `${grouped},${cleaned.slice(sep + 1).replace(/[.,]/g, '').slice(0, 2)}`;
  }
}
