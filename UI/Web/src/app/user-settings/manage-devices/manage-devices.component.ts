import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { Subject, takeUntil } from 'rxjs';
import { Device } from 'src/app/_models/device/device';
import { DevicePlatform, devicePlatforms } from 'src/app/_models/device/device-platform';
import { DeviceService } from 'src/app/_services/device.service';

@Component({
  selector: 'app-manage-devices',
  templateUrl: './manage-devices.component.html',
  styleUrls: ['./manage-devices.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageDevicesComponent implements OnInit, OnDestroy {

  devices: Array<Device> = [];
  addDeviceIsCollapsed: boolean = true;
  device: Device | undefined;


  private readonly onDestroy = new Subject<void>();

  constructor(public deviceService: DeviceService, private toastr: ToastrService, 
    private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.loadDevices();
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  loadDevices() {
    this.addDeviceIsCollapsed = true;
    this.device = undefined;
    this.cdRef.markForCheck();
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
    this.device = device;
    this.addDeviceIsCollapsed = false;
    this.cdRef.markForCheck();
  }
}
