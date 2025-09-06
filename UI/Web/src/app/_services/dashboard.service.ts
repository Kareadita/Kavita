import { Injectable, inject } from '@angular/core';
import {TextResonse} from "../_types/text-response";
import {HttpClient} from "@angular/common/http";
import {environment} from "../../environments/environment";
import {DashboardStream} from "../_models/dashboard/dashboard-stream";

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private httpClient = inject(HttpClient);

  baseUrl = environment.apiUrl;

  getDashboardStreams(visibleOnly = true) {
    return this.httpClient.get<Array<DashboardStream>>(this.baseUrl + 'stream/dashboard?visibleOnly=' + visibleOnly);
  }

  updateDashboardStreamPosition(streamName: string, dashboardStreamId: number, fromPosition: number, toPosition: number) {
    return this.httpClient.post(this.baseUrl + 'stream/update-dashboard-position', {streamName, id: dashboardStreamId, fromPosition, toPosition}, TextResonse);
  }

  updateDashboardStream(stream: DashboardStream) {
    return this.httpClient.post(this.baseUrl + 'stream/update-dashboard-stream', stream, TextResonse);
  }

  createDashboardStream(smartFilterId: number) {
    return this.httpClient.post<DashboardStream>(this.baseUrl + 'stream/add-dashboard-stream?smartFilterId=' + smartFilterId, {});
  }

  deleteSmartFilterStream(streamId: number) {
    return this.httpClient.delete(this.baseUrl + 'stream/smart-filter-dashboard-stream?dashboardStreamId=' + streamId, {});
  }
}
