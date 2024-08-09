import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ReplaySubject, shareReplay, tap } from 'rxjs';
import { environment } from 'src/environments/environment';
import { Device } from '../_models/device/device';
import { DevicePlatform } from '../_models/device/device-platform';
import { TextResonse } from '../_types/text-response';
import { AccountService } from './account.service';

@Injectable({
  providedIn: 'root'
})
export class DeviceService {

  baseUrl = environment.apiUrl;

  private devicesSource: ReplaySubject<Device[]> = new ReplaySubject<Device[]>(1);
  public devices$ = this.devicesSource.asObservable().pipe(shareReplay());


  constructor(private httpClient: HttpClient, private accountService: AccountService) {
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

  createDevice(name: string, platform: DevicePlatform, emailAddress: string) {
    return this.httpClient.post<Device>(this.baseUrl + 'device/create', {name, platform, emailAddress});
  }

  updateDevice(id: number, name: string, platform: DevicePlatform, emailAddress: string) {
    return this.httpClient.post<Device>(this.baseUrl + 'device/update', {id, name, platform, emailAddress});
  }

  deleteDevice(id: number) {
    return this.httpClient.delete(this.baseUrl + 'device?deviceId=' + id);
  }

  getDevices() {
    return this.httpClient.get<Device[]>(this.baseUrl + 'device', {}).pipe(tap(data => {
      this.devicesSource.next(data);
    }));
  }

  sendTo(chapterIds: Array<number>, deviceId: number) {
    return this.httpClient.post(this.baseUrl + 'device/send-to', {deviceId, chapterIds}, TextResonse);
  }

  sendSeriesTo(seriesId: number, deviceId: number) {
    return this.httpClient.post(this.baseUrl + 'device/send-series-to', {deviceId, seriesId}, TextResonse);
  }


}
