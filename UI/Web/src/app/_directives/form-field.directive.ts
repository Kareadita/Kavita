import {computed, DestroyRef, Directive, effect, ElementRef, inject, input, Renderer2} from '@angular/core';
import {AbstractControl} from "@angular/forms";
import {takeUntilDestroyed, toObservable, toSignal} from "@angular/core/rxjs-interop";
import {startWith, switchMap} from "rxjs";

const validTags = ['input', 'select', 'textarea'];

/**
 * Sets .is-invalid + aria-invalid based on the control's state
 */
@Directive({
  selector: '[appFormField]',
})
export class FormFieldDirective {
  private readonly el = inject(ElementRef);
  private readonly renderer = inject(Renderer2);
  private readonly destroyRef = inject(DestroyRef);

  control = input.required<AbstractControl>({alias: 'appFormField'});

  private events = toSignal(
    toObservable(this.control).pipe(
      switchMap(c => c.events.pipe(startWith(null))),
      takeUntilDestroyed(this.destroyRef)
    )
  );
  isInvalid = computed(() => {
    this.events();
    const control = this.control();
    return control.invalid && control.touched;
  });

  constructor() {
    const nativeElem = this.el.nativeElement as HTMLElement;
    const tag = nativeElem?.tagName?.toLowerCase();
    if (!tag || !validTags.includes(tag)) {
      console.warn('appFormField directive is not applicable for ', nativeElem.tagName);
      return;
    }

    effect(() => {
      const isInvalid = this.isInvalid();
      const nativeElem = this.el.nativeElement as HTMLElement;

      if (!nativeElem) return;

      if (isInvalid) {
        this.renderer.addClass(nativeElem, 'is-invalid');
        this.renderer.setAttribute(nativeElem, 'aria-invalid', 'true');
      } else {
        this.renderer.removeClass(nativeElem, 'is-invalid');
        this.renderer.setAttribute(nativeElem, 'aria-invalid', 'false');
      }
    });
  }

}
