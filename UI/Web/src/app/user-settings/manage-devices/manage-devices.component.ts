import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { ReplaySubject, Subject, takeUntil } from 'rxjs';
import { Device } from 'src/app/_models/device/device';
import { DevicePlatform, devicePlatforms } from 'src/app/_models/device/device-platform';
import { DeviceService } from 'src/app/_services/device.service';

@Component({
  selector: 'app-manage-devices',
  templateUrl: './manage-devices.component.html',
  styleUrls: ['./manage-devices.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageDevicesComponent implements OnInit {

  devices: Array<Device> = [];
  addDeviceIsCollapsed: boolean = true;

  settingsForm: FormGroup = new FormGroup({});
  devicePlatforms = devicePlatforms;

  private readonly onDestroy = new Subject<void>();

  constructor(public deviceService: DeviceService, private toastr: ToastrService, 
    private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.settingsForm.addControl('name', new FormControl('', [Validators.required]));
    this.settingsForm.addControl('email', new FormControl('', [Validators.required, Validators.email]));
    this.settingsForm.addControl('platform', new FormControl(DevicePlatform.Custom, [Validators.required]));

    // If user has filled in email and the platform hasn't been explicitly updated, try to update it for them
    this.settingsForm.get('email')?.valueChanges.pipe(takeUntil(this.onDestroy)).subscribe(email => {
      if (this.settingsForm.get('platform')?.dirty) return;
      if (email === null || email === undefined || email === '') return;
      if (email.endsWith('@kindle.com')) this.settingsForm.get('platform')?.setValue(DevicePlatform.Kindle);
      else if (email.endsWith('@pbsync.com')) this.settingsForm.get('platform')?.setValue(DevicePlatform.PocketBook);
      else this.settingsForm.get('platform')?.setValue(DevicePlatform.Custom);
      this.cdRef.markForCheck();
    });

    this.loadDevices();
  }

  loadDevices() {
    this.deviceService.getDevices().subscribe(devices => {
      this.devices = devices;
      this.cdRef.markForCheck();
    });
  }
  
  deleteDevice(device: Device) {
    this.deviceService.deleteDevice(device.id).subscribe(() => {
      const index = this.devices.indexOf(device);
      this.devices.splice(index, 1);
      this.cdRef.markForCheck();
    });
  }

  editDevice(device: Device) {

  }

  addDevice() {
    this.deviceService.createDevice(this.settingsForm.value.name, this.settingsForm.value.platform, this.settingsForm.value.email).subscribe(() => {
      this.settingsForm.reset();
      this.loadDevices();
      this.toastr.success('Device created');
      this.cdRef.markForCheck();
    })
  }

}
