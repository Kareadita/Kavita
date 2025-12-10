import {Pipe, PipeTransform} from '@angular/core';
import {ClientDeviceType} from "../_services/client-info.service";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'clientDeviceClientType'
})
export class ClientDeviceClientTypePipe implements PipeTransform {

  transform(value: ClientDeviceType | undefined | null) {
    if (value === null || value === undefined) return translate('client-device-client-type-pipe.unknown');

    switch (value) {
      case ClientDeviceType.Unknown:
        return translate('client-device-client-type-pipe.unknown');
      case ClientDeviceType.WebBrowser:
        return translate('client-device-client-type-pipe.web-browser');
      case ClientDeviceType.WebApp:
        return translate('client-device-client-type-pipe.web-app');
      case ClientDeviceType.KoReader:
        return translate('client-device-client-type-pipe.koreader');
      case ClientDeviceType.Panels:
        return translate('client-device-client-type-pipe.panels');
      case ClientDeviceType.Librera:
        return translate('client-device-client-type-pipe.librera');
      case ClientDeviceType.OpdsClient:
        return translate('client-device-client-type-pipe.opds-client');

    }
  }

}
