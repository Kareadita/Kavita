import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { environment } from "src/environments/environment";
import { ClientInfo } from "../_models/client-info";

@Injectable({
    providedIn: 'root'
})
export class StatsService {

    baseUrl = environment.apiUrl;

    constructor(private httpClient: HttpClient) { }

    public sendClientInfo(clientInfo: ClientInfo) {
        return this.httpClient.post(this.baseUrl + 'stats/client-info', clientInfo);
    }
}