import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class DeviceService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  createDevice(name: string) {
    return this.httpClient.post(this.baseUrl + 'device/create-web', {}, {responseType: 'text' as 'json'});
  }

  
}
