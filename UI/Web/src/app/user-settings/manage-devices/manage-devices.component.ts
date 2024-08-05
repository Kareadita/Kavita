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
import {NgbCollapse, NgbModal} from '@ng-bootstrap/ng-bootstrap';
import {translate, TranslocoDirective} from "@ngneat/transloco";
import {SettingsService} from "../../admin/settings.service";
import {ConfirmService} from "../../shared/confirm.service";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {ScrobbleEventTypePipe} from "../../_pipes/scrobble-event-type.pipe";
import {SortableHeader} from "../../_single-module/table/_directives/sortable-header.directive";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {EditDeviceModalComponent} from "../_modals/edit-device-modal/edit-device-modal.component";

@Component({
    selector: 'app-manage-devices',
    templateUrl: './manage-devices.component.html',
    styleUrls: ['./manage-devices.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgbCollapse, SentenceCasePipe, DevicePlatformPipe, TranslocoDirective, SettingItemComponent,
    DefaultValuePipe, ScrobbleEventTypePipe, SortableHeader, UtcToLocalTimePipe]
})
export class ManageDevicesComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly deviceService = inject(DeviceService);
  private readonly settingsService = inject(SettingsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(NgbModal);

  devices: Array<Device> = [];
  isEditingDevice: boolean = false;
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
    this.isEditingDevice = false;
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

  addDevice() {
    const ref = this.modalService.open(EditDeviceModalComponent, { scrollable: true, size: 'xl', fullscreen: 'md' });
    ref.componentInstance.device = null;

    ref.closed.subscribe((result: Device | null) => {
      if (result === null) return;

      this.loadDevices();
    });
  }

  editDevice(device: Device) {
    const ref = this.modalService.open(EditDeviceModalComponent, { scrollable: true, size: 'xl', fullscreen: 'md' });
    ref.componentInstance.device = device;

    ref.closed.subscribe((result: Device | null) => {
      if (result === null) return;

      device = result;
      this.cdRef.markForCheck();
    });
  }

}
