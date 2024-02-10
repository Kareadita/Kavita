import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnInit
} from '@angular/core';
import { Device } from 'src/app/_models/device/device';
import { DeviceService } from 'src/app/_services/device.service';
import { DevicePlatformPipe } from '../../_pipes/device-platform.pipe';
import { SentenceCasePipe } from '../../_pipes/sentence-case.pipe';
import { NgIf, NgFor } from '@angular/common';
import { EditDeviceComponent } from '../edit-device/edit-device.component';
import { NgbCollapse } from '@ng-bootstrap/ng-bootstrap';
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {SettingsService} from "../../admin/settings.service";
import {ConfirmService} from "../../shared/confirm.service";

@Component({
    selector: 'app-manage-devices',
    templateUrl: './manage-devices.component.html',
    styleUrls: ['./manage-devices.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [NgbCollapse, EditDeviceComponent, NgIf, NgFor, SentenceCasePipe, DevicePlatformPipe, TranslocoDirective]
})
export class ManageDevicesComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly deviceService = inject(DeviceService);
  private readonly settingsService = inject(SettingsService);
  private readonly confirmService = inject(ConfirmService);

  devices: Array<Device> = [];
  addDeviceIsCollapsed: boolean = true;
  device: Device | undefined;
  hasEmailSetup = false;

  ngOnInit(): void {
    this.settingsService.isEmailSetup().subscribe(res => {
      this.hasEmailSetup = res;
      this.cdRef.markForCheck();
    });
    this.loadDevices();
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

  async deleteDevice(device: Device) {
    if (!await this.confirmService.confirm(translate('toasts.delete-device'))) return;
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
