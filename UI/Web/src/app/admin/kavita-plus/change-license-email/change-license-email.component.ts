import {ChangeDetectionStrategy, Component, inject, output} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {LicenseService} from '../../../_services/license.service';
import {ManageLicenseModalScreen} from '../_modals/manage-license-modal/manage-license-modal-screen';
import {ToastrService} from "ngx-toastr";

@Component({
  selector: 'app-change-license-email',
  imports: [ReactiveFormsModule, TranslocoDirective],
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

  protected readonly form = new FormGroup({
    newEmail: new FormControl('', [Validators.required, Validators.email]),
  });

  sendCode() {
    if (this.form.invalid || !this.licenseInfo()) return;
    this.licenseService.changeEmail(this.licenseInfo()!.registeredEmail, this.form.get('newEmail')!.value!).subscribe(res => {
      this.toastr.info(translate('toasts.change-email-' + (res ? 'success' : 'error')));
    });
  }
}
