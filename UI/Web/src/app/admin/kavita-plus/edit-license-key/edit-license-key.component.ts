import {ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, output, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {ReactiveFormsModule} from "@angular/forms";
import {WikiLink} from "../../../_models/wiki";
import {AccountService} from "../../../_services/account.service";
import {LicenseService} from "../../../_services/license.service";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {email, form, FormField, maxLength, minLength, pattern, required} from "@angular/forms/signals";


export interface LicenseFormEvent {
  licenseKey: string,
  email: string,
  isValid: boolean
}

interface EdiLicenseFormModel {
  licenseKey: string;
  email: string;
}

/**
 * This is the core form logic
 */
@Component({
  selector: 'app-edit-license-key',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    ValidationErrorsComponent,
    FormFieldDirective,
    FormField
  ],
  templateUrl: './edit-license-key.component.html',
  styleUrl: './edit-license-key.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditLicenseKeyComponent {

  protected readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);
  protected readonly destroyRef = inject(DestroyRef);

  /** This will show the additional connect with discord OAuth flow */
  updated = output<LicenseFormEvent>();


  /** This will trigger showing an additional helper to explain why the key is needed again */
  hasLicense = computed(() => this.licenseService.hasActiveLicense());

  private readonly formModel = signal<EdiLicenseFormModel>({
    licenseKey: '',
    email: '',
  });
  formGroup = form(this.formModel, p => {
    required(p.licenseKey);
    maxLength(p.licenseKey, 19);
    minLength(p.licenseKey, 19);
    pattern(p.licenseKey, /^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$/);

    required(p.email);
    email(p.email);
  })


  constructor() {

    const licenseInfo = this.licenseService.licenseInfo();
    if (licenseInfo) {
      this.formGroup.email().value.set(licenseInfo.registeredEmail);
    }

    effect(() => {
      const valid = this.formGroup().valid();
      const model = this.formModel();

      this.updated.emit({...model, isValid: valid});
    });
  }

  protected readonly WikiLink = WikiLink;
  protected readonly form = form;
}
