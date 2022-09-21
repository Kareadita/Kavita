import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { Device } from '../_models/device/device';

@Injectable({
  providedIn: 'root'
})
export class DeviceService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  createDevice(name: string) {
    return this.httpClient.post(this.baseUrl + 'device/create', {}, {responseType: 'text' as 'json'});
  }

  getDevices() {
    return this.httpClient.get<Device[]>(this.baseUrl + 'device', {});
  }

  
}
