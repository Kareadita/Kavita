import { DetailsVersion } from "./details-version";


export interface ClientInfo {
    os: DetailsVersion,
    browser: DetailsVersion,
    platformType: string,
    kavitaUiVersion: string,
    screenResolution: string;
    usingDarkTheme: boolean;

    collectedAt?: Date;
}
