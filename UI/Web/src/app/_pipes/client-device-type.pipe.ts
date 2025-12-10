import { Pipe, PipeTransform } from '@angular/core';
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'clientDeviceType'
})
export class ClientDeviceTypePipe implements PipeTransform {

  transform(value: string | undefined | null) {
    if (value === null || value === undefined) return translate('client-device-type-pipe.unknown');

    if (value === 'mobile') return translate('client-device-type-pipe.mobile');
    if (value === 'desktop') return translate('client-device-type-pipe.desktop');
    if (value === 'tablet') return translate('client-device-type-pipe.tablet');
    return translate('client-device-type-pipe.unknown');
  }

}
