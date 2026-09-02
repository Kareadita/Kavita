import {Component, computed, DestroyRef, inject, input} from '@angular/core';
import {TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {AnyField, toFieldView} from "../../_models/field-view";


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


  control = input.required<AnyField>();
  inputId = input.required<string>();
  /** Overrides for validation error messaging. Map validation -> value. This assumes the value is already localized. */
  messages = input<Record<string, string>>({});

  private readonly view = toFieldView(this.control, this.destroyRef);

  protected readonly validationId = computed(() => {
    return `${this.inputId()}${idPostfix}`
  });

  visibleErrors = computed(() => {

    const overrides = this.messages();
    if (!(this.view.dirty() || this.view.touched())) return []; // Required and touched should show a message


    return this.view.errors().map(e => {
      let title = e.kind;

      if (overrides.hasOwnProperty(e.kind)) {
        title = overrides[e.kind];
      } else if (DEFAULT_MESSAGES.hasOwnProperty(e.kind)) {
        title = this.translocoService.translate(`validation.${DEFAULT_MESSAGES[e.kind]}`, e.params);
      }

      return {
        kind: e.kind,
        title
      }
    });

  });


}
