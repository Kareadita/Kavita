import {HttpClient} from '@angular/common/http';
import {DestroyRef, inject, Injectable, signal} from '@angular/core';
import {EMPTY, switchMap, tap} from 'rxjs';
import {environment} from 'src/environments/environment';
import {Device} from '../_models/device/device';
import {DevicePlatform} from '../_models/device/device-platform';
import {TextResonse} from '../_types/text-response';
import {AccountService} from './account.service';
import {ClientDevice} from "../_models/client-device";
import {map} from "rxjs/operators";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";

@Injectable({
  providedIn: 'root'
})
export class DeviceService {
  private readonly httpClient = inject(HttpClient);
  private readonly accountService = inject(AccountService);
  private readonly destroyRef = inject(DestroyRef);


  readonly baseUrl = environment.apiUrl;



  private readonly _devices = signal<Device[]>([]);
  public readonly devices = this._devices.asReadonly();
  public readonly devices$ = toObservable(this.devices);



  constructor() {

    // Ensure we are authenticated before we make an authenticated api call.
    toObservable(this.accountService.currentUser).pipe(
      switchMap(user => {
        if (!user) {
          this._devices.set([]);
          return EMPTY;
        }
        return this.httpClient.get<Device[]>(this.baseUrl + 'device');
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(data => {
      if (data) this._devices.set([...data])
    });
  }

  createEmailDevice(name: string, platform: DevicePlatform, emailAddress: string) {
    return this.httpClient.post<Device>(this.baseUrl + 'device/create', {name, platform, emailAddress});
  }

  updateEmailDevice(id: number, name: string, platform: DevicePlatform, emailAddress: string) {
    return this.httpClient.post<Device>(this.baseUrl + 'device/update', {id, name, platform, emailAddress});
  }

  deleteEmailDevice(id: number) {
    return this.httpClient.delete(this.baseUrl + 'device?deviceId=' + id);
  }

  getEmailDevices() {
    return this.httpClient.get<Device[]>(this.baseUrl + 'device', {}).pipe(tap(data => {
      this._devices.set([...data]);
    }));
  }

  sendToEmailDevice(chapterIds: Array<number>, deviceId: number) {
    return this.httpClient.post(this.baseUrl + 'device/send-to', {deviceId, chapterIds}, TextResonse);
  }

  sendSeriesToEmailDevice(seriesId: number, deviceId: number) {
    return this.httpClient.post(this.baseUrl + 'device/send-series-to', {deviceId, seriesId}, TextResonse);
  }


  // Client Devices
  getMyClientDevices() {
    return this.httpClient.get<Array<ClientDevice>>(this.baseUrl + 'device/client/devices');
  }

  getAllDevices() {
    return this.httpClient.get<Array<ClientDevice>>(this.baseUrl + 'device/client/all-devices');
  }

  deleteClientDevice(deviceId: number) {
    return this.httpClient.delete(this.baseUrl + 'device/client/device?clientDeviceId=' + deviceId, TextResonse).pipe(map(res => res + '' === 'true'));
  }

  updateClientDeviceName(deviceId: number, name: string) {
    return this.httpClient.post(this.baseUrl + 'device/client/update-name', {name, deviceId});
  }
}
