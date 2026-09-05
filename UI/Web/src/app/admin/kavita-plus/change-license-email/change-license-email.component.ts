import {ChangeDetectionStrategy, Component, inject, output, signal} from '@angular/core';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {LicenseService} from '../../../_services/license.service';
import {ManageLicenseModalScreen} from '../_modals/manage-license-modal/manage-license-modal-screen';
import {ToastrService} from '@openng/ngx-toastr';
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {email, form, FormField, required} from "@angular/forms/signals";

interface ChangeLicenseModel {
  newEmail: string;
}

@Component({
  selector: 'app-change-license-email',
  imports: [TranslocoDirective, FormFieldDirective, ValidationErrorsComponent, FormField],
  templateUrl: './change-license-email.component.html',
  styleUrl: './change-license-email.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangeLicenseEmailComponent implements ManageLicenseModalScreen {

  private readonly licenseService = inject(LicenseService);
  private readonly toastr = inject(ToastrService);

  readonly back = output<void>();
  readonly dismiss = output<void>();

  protected readonly licenseInfo = this.licenseService.licenseInfo;
  private readonly formModel = signal<ChangeLicenseModel>({
    newEmail: ''
  });

  form = form(this.formModel, (path) => {
    required(path.newEmail);
    email(path.newEmail);
  });


  sendCode() {
    if (this.form().invalid() || !this.licenseInfo()) return;
    this.licenseService.changeEmail(this.licenseInfo()!.registeredEmail, this.formModel().newEmail).subscribe(res => {
      this.toastr.info(translate('toasts.change-email-' + (res ? 'success' : 'error')));
    });
  }
}
