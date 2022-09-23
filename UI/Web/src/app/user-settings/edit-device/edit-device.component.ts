import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { Subject, takeUntil } from 'rxjs';
import { Device } from 'src/app/_models/device/device';
import { DevicePlatform, devicePlatforms } from 'src/app/_models/device/device-platform';
import { DeviceService } from 'src/app/_services/device.service';

@Component({
  selector: 'app-edit-device',
  templateUrl: './edit-device.component.html',
  styleUrls: ['./edit-device.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditDeviceComponent implements OnInit, OnChanges, OnDestroy {

  @Input() device: Device | undefined;

  @Output() deviceAdded: EventEmitter<void> = new EventEmitter();
  @Output() deviceUpdated: EventEmitter<Device> = new EventEmitter();

  settingsForm: FormGroup = new FormGroup({});
  devicePlatforms = devicePlatforms;

  private readonly onDestroy = new Subject<void>();

  constructor(public deviceService: DeviceService, private toastr: ToastrService, 
    private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    
    this.settingsForm.addControl('name', new FormControl(this.device?.name || '', [Validators.required]));
    this.settingsForm.addControl('email', new FormControl(this.device?.emailAddress || '', [Validators.required, Validators.email]));
    this.settingsForm.addControl('platform', new FormControl(this.device?.platform || DevicePlatform.Custom, [Validators.required]));

    // If user has filled in email and the platform hasn't been explicitly updated, try to update it for them
    this.settingsForm.get('email')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(email => {
      if (this.settingsForm.get('platform')?.dirty) return;
      if (email === null || email === undefined || email === '') return;
      if (email.endsWith('@kindle.com')) this.settingsForm.get('platform')?.setValue(DevicePlatform.Kindle);
      else if (email.endsWith('@pbsync.com')) this.settingsForm.get('platform')?.setValue(DevicePlatform.PocketBook);
      else this.settingsForm.get('platform')?.setValue(DevicePlatform.Custom);
      this.cdRef.markForCheck();
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (this.device) {
      this.settingsForm.get('name')?.setValue(this.device.name);
      this.settingsForm.get('email')?.setValue(this.device.emailAddress);
      this.settingsForm.get('platform')?.setValue(this.device.platform);
      this.cdRef.markForCheck();
      this.settingsForm.markAsPristine();
    }
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  addDevice() {
    if (this.device !== undefined) {
      this.deviceService.updateDevice(this.device.id, this.settingsForm.value.name, this.settingsForm.value.platform, this.settingsForm.value.email).subscribe(() => {
        this.settingsForm.reset();
        this.toastr.success('Device updated');
        this.cdRef.markForCheck();
        this.deviceUpdated.emit();
      })
      return;
    }

    this.deviceService.createDevice(this.settingsForm.value.name, this.settingsForm.value.platform, this.settingsForm.value.email).subscribe(() => {
      this.settingsForm.reset();
      this.toastr.success('Device created');
      this.cdRef.markForCheck();
      this.deviceAdded.emit();
    });
  }

}
