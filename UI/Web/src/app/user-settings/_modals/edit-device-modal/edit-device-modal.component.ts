import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  Input,
  OnInit,
} from '@angular/core';
import {DeviceService} from "../../../_services/device.service";
import {ToastrService} from "ngx-toastr";
import {Device} from "../../../_models/device/device";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {DevicePlatform, devicePlatforms} from "../../../_models/device/device-platform";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {NgbActiveModal, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {DevicePlatformPipe} from "../../../_pipes/device-platform.pipe";
import {Select2Module} from "ng-select2-component";

@Component({
  selector: 'app-edit-device-modal',
  standalone: true,
  imports: [
    TranslocoDirective,
    DevicePlatformPipe,
    ReactiveFormsModule,
    Select2Module,
    NgbTooltip
  ],
  templateUrl: './edit-device-modal.component.html',
  styleUrl: './edit-device-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditDeviceModalComponent implements OnInit {
  protected readonly deviceService = inject(DeviceService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly modalRef = inject(NgbActiveModal);

  @Input() device: Device | null = null;

  settingsForm: FormGroup = new FormGroup({});
  devicePlatforms = devicePlatforms;

  ngOnInit(): void {

    this.settingsForm.addControl('name', new FormControl(this.device?.name || '', [Validators.required]));
    this.settingsForm.addControl('email', new FormControl(this.device?.emailAddress || '', [Validators.required, Validators.email]));
    this.settingsForm.addControl('platform', new FormControl(this.device?.platform || DevicePlatform.Custom, [Validators.required]));

    // If user has filled in email and the platform hasn't been explicitly updated, try to update it for them
    this.settingsForm.get('email')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(email => {
      if (this.settingsForm.get('platform')?.dirty) return;
      if (email === null || email === undefined || email === '') return;
      if (email.endsWith('@kindle.com')) this.settingsForm.get('platform')?.setValue(DevicePlatform.Kindle);
      else if (email.endsWith('@pbsync.com')) this.settingsForm.get('platform')?.setValue(DevicePlatform.PocketBook);
      else this.settingsForm.get('platform')?.setValue(DevicePlatform.Custom);
      this.cdRef.markForCheck();
    });
  }

  save() {
    if (this.device !== null) {
      this.deviceService.updateDevice(this.device.id, this.settingsForm.value.name, parseInt(this.settingsForm.value.platform, 10), this.settingsForm.value.email)
        .subscribe((device) => {
          this.settingsForm.reset();
          this.toastr.success(translate('toasts.device-updated'));
          this.cdRef.markForCheck();
          this.close(device);
      });
      return;
    }

    this.deviceService.createDevice(this.settingsForm.value.name, parseInt(this.settingsForm.value.platform, 10), this.settingsForm.value.email)
      .subscribe((device) => {
        this.settingsForm.reset();
        this.toastr.success(translate('toasts.device-created'));
        this.cdRef.markForCheck();
        this.close(device);
    });
  }

  close(device: Device | null = null) {
    this.modalRef.close(device);
  }
}
