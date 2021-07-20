import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import * as Bowser from "bowser";
import { take } from "rxjs/operators";
import { environment } from "src/environments/environment";
import { ClientInfo } from "../_models/stats/client-info";
import { DetailsVersion } from "../_models/stats/details-version";
import { NavService } from "./nav.service";
import { version } from '../../../package.json';


@Injectable({
    providedIn: 'root'
})
export class StatsService {

    baseUrl = environment.apiUrl;

    constructor(private httpClient: HttpClient, private navService: NavService) { }

    public sendClientInfo(data: ClientInfo) {
        return this.httpClient.post(this.baseUrl + 'stats/client-info', data);
    }

    public async getInfo(): Promise<ClientInfo> {
        const screenResolution = `${window.screen.width} x ${window.screen.height}`;

        const browser = Bowser.getParser(window.navigator.userAgent);

        const usingDarkTheme = await this.navService.darkMode$.pipe(take(1)).toPromise();

        return {
            os: browser.getOS() as DetailsVersion,
            browser: browser.getBrowser() as DetailsVersion,
            platformType: browser.getPlatformType(),
            kavitaUiVersion: version,
            screenResolution,
            usingDarkTheme
        };
    }
}