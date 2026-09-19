import {ChangeDetectionStrategy, Component, effect, inject, input, signal,} from '@angular/core';
import {DeviceService} from "../../../_services/device.service";
import {ToastrService} from '@openng/ngx-toastr';
import {Device} from "../../../_models/device/device";
import {allDevicePlatforms, DevicePlatform} from "../../../_models/device/device-platform";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {NgbActiveModal, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {DevicePlatformPipe} from "../../../_pipes/device-platform.pipe";
import {modalSaved} from "../../../_models/modal/modal-result";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {email, form, FormField, required} from "@angular/forms/signals";

interface FormModel {
  name: string;
  emailAddress: string;
  platform: string;
}

@Component({
  selector: 'app-edit-device-modal',
  imports: [
    TranslocoDirective,
    DevicePlatformPipe,
    NgbTooltip,
    FormFieldDirective,
    ValidationErrorsComponent,
    FormField
  ],
  templateUrl: './edit-device-modal.component.html',
  styleUrl: './edit-device-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditDeviceModalComponent {
  protected readonly deviceService = inject(DeviceService);
  private readonly toastr = inject(ToastrService);
  private readonly modalRef = inject(NgbActiveModal);

  device = input<Device | null>(null);

  private readonly formModel = signal<FormModel>({
    name: '', emailAddress: '', platform: DevicePlatform.Custom.toString()
  });
  formGroup = form(this.formModel, p => {
    required(p.name);
    required(p.emailAddress);
    email(p.emailAddress);
    required(p.platform);
  });

  constructor() {
    effect(() => {
      const d = this.device();
      if (!d) return;

      this.formGroup.name().value.set(d.name);
      this.formGroup.emailAddress().value.set(d.emailAddress);
      this.formGroup.platform().value.set(d.platform.toString());
    });

    effect(() => {
      // If user has filled in email and the platform hasn't been explicitly updated, try to update it for them
      const isPlatformDirty = this.formGroup.platform().dirty();
      const email = this.formGroup.emailAddress().value();
      if (isPlatformDirty) return;
      if (email === null || email === undefined || email === '') return;

      if (email.endsWith('@kindle.com')) {
        this.formGroup.platform().value.set(DevicePlatform.Kindle.toString());
      } else if (email.endsWith('@pbsync.com')) {
        this.formGroup.platform().value.set(DevicePlatform.PocketBook.toString());
      } else {
        this.formGroup.platform().value.set(DevicePlatform.Custom.toString());
      }
    });
  }


  save() {
    const device = this.device();
    const model = this.formModel();
    if (device !== null) {
      this.deviceService.updateEmailDevice(device.id, model.name, parseInt(model.platform, 10), model.emailAddress)
        .subscribe((device) => {
          this.formGroup().reset();
          this.toastr.success(translate('toasts.device-updated'));
          this.close(device);
      });
      return;
    }

    this.deviceService.createEmailDevice(model.name, parseInt(model.platform, 10), model.emailAddress)
      .subscribe((device) => {
        this.formGroup().reset();
        this.toastr.success(translate('toasts.device-created'));
        this.close(device);
    });
  }

  close(device: Device | null = null) {
    if (device !== null) {
      this.modalRef.close(modalSaved(device));
      return;
    }
    this.modalRef.dismiss();
  }

  protected readonly devicePlatforms = allDevicePlatforms;
}
