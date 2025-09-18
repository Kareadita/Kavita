import { Injectable, inject } from '@angular/core';
import {environment} from "../../environments/environment";
import { HttpClient } from "@angular/common/http";
import {Volume} from "../_models/volume";
import {TextResonse} from "../_types/text-response";

@Injectable({
  providedIn: 'root'
})
export class VolumeService {
  private httpClient = inject(HttpClient);


  baseUrl = environment.apiUrl;

  getVolumeMetadata(volumeId: number) {
    return this.httpClient.get<Volume>(this.baseUrl + 'volume?volumeId=' + volumeId);
  }

  deleteVolume(volumeId: number) {
    return this.httpClient.delete<boolean>(this.baseUrl + 'volume?volumeId=' + volumeId);
  }

  deleteMultipleVolumes(volumeIds: number[]) {
    return this.httpClient.post<boolean>(this.baseUrl + "volume/multiple", volumeIds)
  }

  updateVolume(volume: any) {
    return this.httpClient.post(this.baseUrl + 'volume/update', volume, TextResonse);
  }

}
