import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";
import {DevicePlatform} from "../_models/device/device-platform";

@Pipe({
    name: 'devicePlatform',
    standalone: true
})
export class DevicePlatformPipe implements PipeTransform {

  readonly translocoService = inject(TranslocoService);

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
