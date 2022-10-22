import { Pipe, PipeTransform } from '@angular/core';
import { DevicePlatform } from 'src/app/_models/device/device-platform';

@Pipe({
  name: 'devicePlatform'
})
export class DevicePlatformPipe implements PipeTransform {

  transform(value: DevicePlatform): string {
    switch(value) {
      case DevicePlatform.Kindle: return 'Kindle';
      case DevicePlatform.Kobo: return 'Kobo';
      case DevicePlatform.PocketBook: return 'PocketBook';
      case DevicePlatform.Custom: return 'Custom';
      default: return value + '';
    }
  }

}
