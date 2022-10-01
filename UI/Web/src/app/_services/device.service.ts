import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ReplaySubject, shareReplay, switchMap, take, tap } from 'rxjs';
import { environment } from 'src/environments/environment';
import { Device } from '../_models/device/device';
import { DevicePlatform } from '../_models/device/device-platform';

@Injectable({
  providedIn: 'root'
})
export class DeviceService {

  baseUrl = environment.apiUrl;

  private devicesSource: ReplaySubject<Device[]> = new ReplaySubject<Device[]>(1);
  public devices$ = this.devicesSource.asObservable().pipe(shareReplay());


  constructor(private httpClient: HttpClient) {
    this.httpClient.get<Device[]>(this.baseUrl + 'device', {}).subscribe(data => {
      this.devicesSource.next(data);
    });
  }

  createDevice(name: string, platform: DevicePlatform, emailAddress: string) {
    return this.httpClient.post(this.baseUrl + 'device/create', {name, platform, emailAddress}, {responseType: 'text' as 'json'});
  }

  updateDevice(id: number, name: string, platform: DevicePlatform, emailAddress: string) {
    return this.httpClient.post(this.baseUrl + 'device/update', {id, name, platform, emailAddress}, {responseType: 'text' as 'json'});
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
    return this.httpClient.post(this.baseUrl + 'device/send-to', {deviceId, chapterIds}, {responseType: 'text' as 'json'});
  }

  
}
