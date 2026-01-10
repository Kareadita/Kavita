import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {Device} from 'src/app/_models/device/device';
import {DeviceService} from 'src/app/_services/device.service';
import {DevicePlatformPipe} from '../../_pipes/device-platform.pipe';
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
import {NgxDatatableModule} from "@siemens/ngx-datatable";
import {AsyncPipe} from "@angular/common";
import {ClientDevice} from "../../_models/client-device";
import {ClientDeviceCardComponent} from "../../_single-module/client-device-card/client-device-card.component";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";

@Component({
    selector: 'app-manage-devices',
    templateUrl: './manage-devices.component.html',
    styleUrls: ['./manage-devices.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DevicePlatformPipe, TranslocoDirective, AsyncPipe, NgxDatatableModule, ClientDeviceCardComponent, LoadingComponent, ResponsiveTableComponent]
})
export class ManageDevicesComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly deviceService = inject(DeviceService);
  private readonly settingsService = inject(SettingsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly modalService = inject(NgbModal);
  private readonly accountService = inject(AccountService);

  //devices: Array<Device> = [];
  devices = signal<Device[]>([]);
  hasEmailSetup = signal<boolean>(false);
  trackBy = (idx: number, item: Device) => `${item.name}_${item.emailAddress}_${item.platform}_${item.lastUsed}`;

  clientDevices = signal<ClientDevice[]>([]);


  isReadOnly$ = this.accountService.currentUser$.pipe(
    takeUntilDestroyed(this.destroyRef),
    map(c => c && this.accountService.hasReadOnlyRole(c)),
    shareReplay({refCount: true, bufferSize: 1}),
  );

  constructor() {
    this.loadClientDevices();
  }

  ngOnInit(): void {
    this.settingsService.isEmailSetup().subscribe(res => {
      this.hasEmailSetup.set(res);
    });
    this.loadDevices();
  }

  loadClientDevices() {
    this.deviceService.getMyClientDevices().subscribe(devices => {
      this.clientDevices.set([...devices]);
    });
  }

  loadDevices() {
    this.deviceService.getEmailDevices().subscribe(devices => {
      this.devices.set([...devices]);
    });
  }

  async deleteDevice(device: Device) {
    if (!await this.confirmService.confirm(translate('toasts.delete-device'))) return;
    this.deviceService.deleteEmailDevice(device.id).subscribe(() => {
      const oldDevices = this.devices();
      const index = oldDevices.indexOf(device);

      oldDevices.splice(index, 1);
      this.devices.set([...oldDevices]);
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
}
