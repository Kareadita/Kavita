import {Directive, effect, ElementRef, HostListener, inject, input, OnInit, output, signal} from '@angular/core';

@Directive({
  selector: '[appToggleVisibility]',
  standalone: true,
  host: {
    'role': 'button',
    'tabindex': '0',
    '[attr.aria-label]': 'ariaLabel()',
    '[style.cursor]': '"pointer"',
    '[style.user-select]': '"none"',
    '(keydown)': 'onKeydown($event)'
  }
})
export class ToggleVisibilityDirective implements OnInit {

  private readonly elementRef: ElementRef = inject(ElementRef);

  value = input.required<string>({ alias: 'appToggleVisibility' });
  maskChar = input<string>('•');

  visibilityChanged = output<boolean>();

  private readonly isVisible = signal(false);
  readonly ariaLabel = signal('Show sensitive information');

  constructor() {
    effect(() => {
      this.updateDisplay();
    });
  }

  ngOnInit(): void {
    this.updateDisplay();
  }

  @HostListener('click')
  toggle(): void {
    this.isVisible.update(visible => !visible);
    this.ariaLabel.set(this.isVisible() ? 'Hide sensitive information' : 'Show sensitive information');
    this.visibilityChanged.emit(this.isVisible());
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.toggle();
    }
  }

  private updateDisplay(): void {
    const displayValue = this.isVisible()
      ? this.value()
      : this.maskChar().repeat(this.value().length);

    this.elementRef.nativeElement.textContent = displayValue;
  }

}
