import {HttpClient} from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import {ReplaySubject, shareReplay, tap} from 'rxjs';
import {environment} from 'src/environments/environment';
import {Device} from '../_models/device/device';
import {DevicePlatform} from '../_models/device/device-platform';
import {TextResonse} from '../_types/text-response';
import {AccountService} from './account.service';
import {ClientDevice} from "../_models/client-device";
import {map} from "rxjs/operators";

@Injectable({
  providedIn: 'root'
})
export class DeviceService {
  private httpClient = inject(HttpClient);
  private accountService = inject(AccountService);


  baseUrl = environment.apiUrl;

  private readonly devicesSource: ReplaySubject<Device[]> = new ReplaySubject<Device[]>(1);
  public readonly devices$ = this.devicesSource.asObservable().pipe(shareReplay());



  constructor() {
    // Ensure we are authenticated before we make an authenticated api call.
    this.accountService.currentUser$.subscribe(user => {
      if (!user) {
        this.devicesSource.next([]);
        return;
      }

      this.httpClient.get<Device[]>(this.baseUrl + 'device', {}).subscribe(data => {
        this.devicesSource.next(data);
      });
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
      this.devicesSource.next(data);
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
