import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { ReplaySubject, Subject } from 'rxjs';
import { Device } from 'src/app/_models/device/device';
import { DeviceService } from 'src/app/_services/device.service';

@Component({
  selector: 'app-manage-devices',
  templateUrl: './manage-devices.component.html',
  styleUrls: ['./manage-devices.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageDevicesComponent implements OnInit {

  devices: Array<Device> = [];

  private readonly onDestroy = new Subject<void>();

  constructor(public deviceService: DeviceService, private toastr: ToastrService, 
    private readonly cdRef: ChangeDetectorRef) { }

  ngOnInit(): void {
    this.devices$ = this.deviceService.getDevices();
  }
  
  deleteDevice(device: Device) {
    
  }

}
