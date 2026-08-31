import {Component, computed, DestroyRef, inject, input} from '@angular/core';
import {TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {AbstractControl} from "@angular/forms";
import {takeUntilDestroyed, toObservable, toSignal} from "@angular/core/rxjs-interop";
import {startWith, switchMap} from "rxjs";


const DEFAULT_MESSAGES: Record<string, string> = {
  required: 'required-field',
  email: 'email-invalid',
  minlength: 'min-length',
  validEmail: 'valid-email',
  passwordValidation: 'password-validation',
  yearValidation: 'year-validation',
  invalidUri: 'invalid-uri',
  min: 'min',
  max: 'max',
  invalidAsin: 'invalid-asin',
  invalidAmazonCode: 'invalid-amazon-code',
};

/** postfix for aria-describedby */
export const idPostfix = '-validations';


@Component({
  imports: [
    TranslocoDirective
  ],
  selector: 'app-validation-errors',
  styleUrl: './validation-errors.component.scss',
  templateUrl: './validation-errors.component.html',
})
export class ValidationErrorsComponent {

  private readonly destroyRef = inject(DestroyRef);
  private readonly translocoService = inject(TranslocoService);


  control = input.required<AbstractControl>();
  inputId = input.required<string>();
  /** Overrides for validation error messaging. Map validation -> value. This assumes the value is already localized. */
  messages = input<Record<string, string>>({});

  protected readonly validationId = computed(() => {
    return `${this.inputId()}${idPostfix}`
  })

  private events = toSignal(
    toObservable(this.control).pipe(
      switchMap(c => c.events.pipe(startWith(null))),
      takeUntilDestroyed(this.destroyRef)
    )
  );


  visibleErrors = computed(() => {
    this.events();
    const control = this.control();
    const overrides = this.messages();
    if (!control.errors || !(control.dirty || control.touched)) return []; // Required and touched should show a message

    return Object.keys(control.errors).map(key => {
      const value = control.errors![key];
      let title = key;

      if (overrides.hasOwnProperty(key)) {
        title = overrides[key];
      } else if (DEFAULT_MESSAGES.hasOwnProperty(key)) {
        title = this.translocoService.translate(`validation.${DEFAULT_MESSAGES[key]}`, typeof value === 'object' ? value : {});
      }

      return {
        key,
        title
      }
    });

  });


}
