import {inject, Pipe, PipeTransform} from '@angular/core';
import { DevicePlatform } from 'src/app/_models/device/device-platform';
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
    name: 'devicePlatform',
    standalone: true
})
export class DevicePlatformPipe implements PipeTransform {

  translocoService = inject(TranslocoService);

  transform(value: DevicePlatform): string {
    switch(value) {
      case DevicePlatform.Kindle: return 'Kindle';
      case DevicePlatform.Kobo: return 'Kobo';
      case DevicePlatform.PocketBook: return 'PocketBook';
      case DevicePlatform.Custom: return this.translocoService.translate('device-platform-pipe.custom');
      default: return value + '';
    }
  }

}
