import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { Device } from '../_models/device/device';
import { DevicePlatform } from '../_models/device/device-platform';

@Injectable({
  providedIn: 'root'
})
export class DeviceService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  createDevice(name: string, platform: DevicePlatform, emailAddress: string) {
    return this.httpClient.post(this.baseUrl + 'device/create', {name, platform, emailAddress}, {responseType: 'text' as 'json'});
  }

  deleteDevice(id: number) {
    return this.httpClient.delete(this.baseUrl + 'device?deviceId=' + id);
  }

  getDevices() {
    return this.httpClient.get<Device[]>(this.baseUrl + 'device', {});
  }

  sendTo(chapterId: number, deviceId: number) {
    return this.httpClient.post(this.baseUrl + 'device/send-to', {deviceId, chapterId}, {responseType: 'text' as 'json'});
  }

  
}
