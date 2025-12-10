import {Pipe, PipeTransform} from '@angular/core';
import {ClientDevicePlatform} from "../_services/client-info.service";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'clientDevicePlatform'
})
export class ClientDevicePlatformPipe implements PipeTransform {

  transform(value: ClientDevicePlatform | undefined | null) {
    if (value === null || value === undefined) return translate('client-device-platform-pipe.unknown');

    switch (value) {
      case ClientDevicePlatform.Unknown:
        return translate('client-device-platform-pipe.unknown');
      case ClientDevicePlatform.Windows:
        return 'Windows';
      case ClientDevicePlatform.MacOs:
        return 'macOs';
      case ClientDevicePlatform.Ios:
        return 'iOS';
      case ClientDevicePlatform.Linux:
        return 'Linux';
      case ClientDevicePlatform.Android:
        return 'Android';

    }
  }

}
