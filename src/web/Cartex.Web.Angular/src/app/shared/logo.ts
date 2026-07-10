import { Component, computed, input } from '@angular/core';

const C_PATH =
  'M245 24L267 24L299 28L316 32L351 45L389 68L407 83L427 104L426 107L367 172L355 158L333 138L317 128L293 118L275 114L246 113L210 121L183 135L170 145L153 162L137 185L128 204L122 223L118 249L118 271L123 301L132 325L144 346L155 360L179 382L201 395L232 405L246 407L282 405L300 400L325 388L344 374L366 350L429 413L423 422L401 443L365 468L342 479L317 488L271 496L224 494L184 484L145 466L129 456L107 438L89 420L74 401L64 386L50 359L37 321L30 280L31 232L41 185L62 138L87 103L120 71L158 47L193 33L213 28Z';
const X_PATH =
  'M602 51L718 52L717 56L681 102L555 260L556 265L709 455L718 467L717 470L596 469L495 338L492 338L488 342L458 380L448 391L446 391L384 329L384 325L388 319L433 263L428 253L385 199L384 196L388 189L443 128L446 128L449 131L488 180L494 185Z';

@Component({
  selector: 'cx-logo',
  template: `
    <span class="cx-logo" [style.height.px]="size()">
      <svg [attr.width]="width()" [attr.height]="size()" viewBox="0 0 751 529">
        <path [attr.d]="cPath" fill="currentColor" />
        <path [attr.d]="xPath" fill="#255D3A" />
      </svg>
      @if (wordmark()) {
        <span class="cx-logo-text" [style.font-size.px]="size() * 0.56">CARTEX</span>
      }
    </span>
  `,
  styles: `
    .cx-logo { display: inline-flex; align-items: center; gap: 10px; color: inherit; }
    .cx-logo-text { font-weight: 700; letter-spacing: 0.02em; line-height: 1; color: var(--cx-brand); }
  `,
})
export class Logo {
  readonly size = input(32);
  readonly wordmark = input(false);
  readonly width = computed(() => Math.round(this.size() * (751 / 529)));
  readonly cPath = C_PATH;
  readonly xPath = X_PATH;
}
