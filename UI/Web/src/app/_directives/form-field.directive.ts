import {
  computed,
  DestroyRef,
  Directive,
  effect,
  ElementRef,
  HostListener,
  inject,
  input,
  Renderer2
} from '@angular/core';
import {AnyField, toFieldView} from "../shared/_models/field-view";
import {isFieldTree} from "@angular/forms/signals";
import {AbstractControl} from "@angular/forms";

const validTags = ['input', 'select', 'textarea'];

/**
 * Wires a native form element to a field: reflects invalid state, and reports blur as touched (selects need for validation outline)
 */
@Directive({
  selector: '[appFormField]',
})
export class FormFieldDirective {
  private readonly el = inject(ElementRef);
  private readonly renderer = inject(Renderer2);
  private readonly destroyRef = inject(DestroyRef);

  control = input.required<AnyField>({alias: 'appFormField'});

  private readonly view = toFieldView(this.control, this.destroyRef);

  isInvalid = computed(() => {
    return this.view.invalid() && this.view.touched();
  });

  @HostListener('blur')
  onBlur() {
    if (this.view.touched()) return;

    const c = this.control();
    if (isFieldTree(c)) {
      c().markAsTouched();
    } else {
      (c as AbstractControl).markAsTouched();
    }
  }


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
