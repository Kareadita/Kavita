import * as Bowser from "bowser";
import { version } from '../../../../package.json';
import { ClientInfo } from "src/app/_models/client-info";
import { DetailsVersion } from "src/app/_models/details-version";

const getClientInfo = (): ClientInfo => {

    const screenResolution = `${window.screen.width} x ${window.screen.height}`;

    const browser = Bowser.getParser(window.navigator.userAgent);

    return {
        os: browser.getOS() as DetailsVersion,
        browser: browser.getBrowser() as DetailsVersion,
        platformType: browser.getPlatformType(),
        kavitaUiVersion: version,
        screenResolution
    };
}

export { getClientInfo };