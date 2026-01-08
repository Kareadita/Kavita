import {ChangeDetectionStrategy, Component, computed, HostListener, inject, input, model, output} from '@angular/core';
import {ClientDevice} from "../../_models/client-device";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {TimeAgoPipe} from "../../_pipes/time-ago.pipe";
import {ClientDeviceType, ClientInfoService} from "../../_services/client-info.service";
import {ClientDeviceAuthTypePipe} from "../../_pipes/client-device-authtype.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {ClientDevicePlatformPipe} from "../../_pipes/client-device-platform.pipe";
import {ClientDeviceClientTypePipe} from "../../_pipes/client-device-client-type.pipe";
import {DateTime, Duration, Interval} from "luxon";
import {UtcToLocalDatePipe} from "../../_pipes/utc-to-locale-date.pipe";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {CardActionablesComponent} from "../card-actionables/card-actionables.component";
import {Action, ActionFactoryService, ActionItem} from "../../_services/action-factory.service";
import {SentenceCasePipe} from "../../_pipes/sentence-case.pipe";
import {DeviceService} from "../../_services/device.service";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {DOCUMENT} from "@angular/common";
import {AccountService} from "../../_services/account.service";
import {User} from "../../_models/user/user";
import {Breakpoint, UtilityService} from "../../shared/_services/utility.service";

@Component({
  selector: 'app-client-device-card',
  imports: [
    TranslocoDirective,
    TimeAgoPipe,
    ClientDeviceAuthTypePipe,
    DefaultValuePipe,
    ClientDevicePlatformPipe,
    ClientDeviceClientTypePipe,
    UtcToLocalDatePipe,
    DefaultDatePipe,
    UtcToLocalTimePipe,
    CardActionablesComponent,
    SentenceCasePipe,
    ReactiveFormsModule
  ],
  templateUrl: './client-device-card.component.html',
  styleUrl: './client-device-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ClientDeviceCardComponent {

  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly deviceService = inject(DeviceService);
  private readonly document = inject(DOCUMENT);
  private readonly accountService = inject(AccountService);
  private readonly utilityService = inject(UtilityService);
  protected readonly clientInfoService = inject(ClientInfoService);

  clientDevice = input.required<ClientDevice>();

  actions = model<ActionItem<ClientDevice>[]>([]);
  isEditMode = model<boolean>(false);

  /**
   * Device is deleted
   */
  deviceDeleted = output<number>();
  /**
   * Name is updated
   */
  refresh = output<number>();

  deviceForm = new FormGroup({
    name: new FormControl('', [Validators.required]),
  });

  currentUserId = computed(() => {
    return this.accountService.currentUserSignal()?.id;
  });

  ipAddress = computed(() => {
    const address = this.clientDevice().currentClientInfo.ipAddress;
    if (!address) return null;
    if (address === '::1' || address === '::ffff:127.0.0.1') return translate('client-device-card.local');
    return address;
  })

  browserInfo = computed(() => {
    const info = this.clientDevice().currentClientInfo;
    if (!info.browser && !info.browserVersion) return '';

    return `${info.browser} ${info.browserVersion}`;
  });

  browserIcon = computed(() => {
    const browser = this.clientDevice().currentClientInfo.browser;
    if (!browser) return 'fa-brands fa-chrome';
    switch (browser) {
      case 'Chrome':
        return 'fa-brands fa-chrome';
      case 'Firefox':
        return 'fa-brands fa-firefox';
      case 'Safari':
        return 'fa-brands fa-safari';
      case 'Edge':
        return 'fa-brands fa-edge';
      default:
        return 'fa-brands fa-chrome';
    }
  });

  screenInfo = computed(() => {
    const info = this.clientDevice().currentClientInfo;
    if (!info.screenWidth && !info.screenHeight) return '';
    return `${info.screenWidth}×${info.screenHeight} (${info.orientation})`;
  });

  deviceIcon = computed(() => {
    if (this.clientDevice().currentClientInfo?.deviceType != null) {
      const deviceType = this.clientDevice().currentClientInfo.deviceType.toLowerCase();
      if (deviceType.includes('mobile')) return 'fa-solid fa-mobile-screen';
      if (deviceType.includes('tablet')) return 'fa-solid fa-tablet-screen-button';
      if (deviceType.includes('laptop')) return 'fa-solid fa-laptop';
    }

    return 'fa-solid fa-desktop';
  });

  clientTypeBadgeClass = computed(() => {
    const clientType = this.clientDevice().currentClientInfo.clientType;
    if (clientType == ClientDeviceType.WebApp || clientType === ClientDeviceType.WebBrowser) return 'badge bg-primary';
    if ([ClientDeviceType.OpdsClient, ClientDeviceType.Librera].includes(clientType)) return 'badge bg-info';
    return 'badge bg-secondary';
  });

  isRecentlyActive = computed(() => {
    const lastSeen = DateTime.fromISO(this.clientDevice().lastSeenUtc, { zone: "utc" });
    const now = DateTime.now().toUTC();
    const twentyFourHours = Duration.fromObject({ hours: 24 });

    const interval = Interval.fromDateTimes(lastSeen, now);

    return interval.toDuration().valueOf() < twentyFourHours.valueOf();
  });

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.isEditMode()) return;

    const control = this.deviceForm.get('name');
    if (control?.invalid) return;

    const inputElem = this.document.querySelector(`#client-device-${this.clientDevice().id}`);
    const clickedInside = inputElem?.contains(event.target as Node);

    if (!clickedInside) {
      this.isEditMode.set(false);
    }
  }


  constructor() {
    const user = this.accountService.currentUserSignal();
    if (user && !this.accountService.hasReadOnlyRole(user)) {
      this.actions.set(this.actionFactoryService.getClientDeviceActions(this.handleActionCallback.bind(this), this.shouldRenderAction.bind(this)));
    }
  }


  shouldRenderAction(action: ActionItem<ClientDevice>, entity: ClientDevice, user: User) {
    const loggedInUser = this.accountService.currentUserSignal();
    return entity.ownerUserId === loggedInUser?.id; // Only a user can manipulate their own devices
  }


  handleActionCallback(action: ActionItem<ClientDevice>, entity: ClientDevice) {
    switch (action.action) {
      case Action.Delete:
        this.deleteDevice();
        break;
      case Action.Edit:
        // The actionable modal needs some time to clean up
        if (this.utilityService.activeBreakpointSignal()! < Breakpoint.Tablet ) {
          setTimeout(() => this.toggleEdit(), 100);
        } else {
          this.toggleEdit();
        }
        break;
    }
  }

  saveName() {
    const newName = this.deviceForm.get('name')?.value ?? ''
    if (newName.length === 0) return;
    this.deviceService.updateClientDeviceName(this.clientDevice().id, newName).subscribe(() => {
      this.refresh.emit(this.clientDevice().id);
    });
  }

  deleteDevice() {
    const id = this.clientDevice().id;
    this.deviceService.deleteClientDevice(id).subscribe(successful => {
      if (successful) {
        this.deviceDeleted.emit(id);
      }
    });
  }

  toggleEdit() {
    this.deviceForm.get('name')!.setValue(this.clientDevice().friendlyName);
    this.isEditMode.update(x => !x);
  }



}
