import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  inject,
  OnInit
} from '@angular/core';
import { Device } from 'src/app/_models/device/device';
import { DeviceService } from 'src/app/_services/device.service';
import { DevicePlatformPipe } from '../../_pipes/device-platform.pipe';
import {NgbModal} from '@ng-bootstrap/ng-bootstrap';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingsService} from "../../admin/settings.service";
import {ConfirmService} from "../../shared/confirm.service";
import {EditDeviceModalComponent} from "../_modals/edit-device-modal/edit-device-modal.component";
import {DefaultModalOptions} from "../../_models/default-modal-options";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map} from "rxjs";
import {shareReplay} from "rxjs/operators";
import {AccountService} from "../../_services/account.service";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {AsyncPipe} from "@angular/common";

@Component({
    selector: 'app-manage-devices',
    templateUrl: './manage-devices.component.html',
    styleUrls: ['./manage-devices.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [DevicePlatformPipe, TranslocoDirective, AsyncPipe, NgxDatatableModule]
})
export class ManageDevicesComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly deviceService = inject(DeviceService);
  private readonly settingsService = inject(SettingsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(NgbModal);
  private readonly accountService = inject(AccountService);

  devices: Array<Device> = [];
  isEditingDevice: boolean = false;
  device: Device | undefined;
  hasEmailSetup = false;

  isReadOnly$ = this.accountService.currentUser$.pipe(
    takeUntilDestroyed(this.destroyRef),
    map(c => c && this.accountService.hasReadOnlyRole(c)),
    shareReplay({refCount: true, bufferSize: 1}),
  );

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
    const ref = this.modalService.open(EditDeviceModalComponent, DefaultModalOptions);
    ref.componentInstance.device = null;

    ref.closed.subscribe((result: Device | null) => {
      if (result === null) return;

      this.loadDevices();
    });
  }

  editDevice(device: Device) {
    const ref = this.modalService.open(EditDeviceModalComponent, DefaultModalOptions);
    ref.componentInstance.device = device;

    ref.closed.subscribe((result: Device | null) => {
      if (result === null) return;

      device = result;
      this.cdRef.markForCheck();
    });
  }

    protected readonly ColumnMode = ColumnMode;
}
