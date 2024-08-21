import {Pipe, PipeTransform} from '@angular/core';
import {ScrobbleProvider} from "../_services/scrobbling.service";

@Pipe({
  name: 'providerImage',
  standalone: true
})
export class ProviderImagePipe implements PipeTransform {

  transform(value: ScrobbleProvider, large: boolean = false): string {
    switch (value) {
      case ScrobbleProvider.AniList:
        return `assets/images/ExternalServices/AniList${large ? '-lg' : ''}.png`;
      case ScrobbleProvider.Mal:
        return `assets/images/ExternalServices/MAL${large ? '-lg' : ''}.png`;
      case ScrobbleProvider.GoogleBooks:
        return `assets/images/ExternalServices/GoogleBooks${large ? '-lg' : ''}.png`;
      case ScrobbleProvider.Kavita:
        return `assets/images/logo-${large ? '64' : '32'}.png`;
    }
  }

}
