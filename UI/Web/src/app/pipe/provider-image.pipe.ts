import {Pipe, PipeTransform} from '@angular/core';
import {ScrobbleProvider} from "../_services/scrobbling.service";

@Pipe({
  name: 'providerImage',
  standalone: true
})
export class ProviderImagePipe implements PipeTransform {

  transform(value: ScrobbleProvider): string {
    switch (value) {
      case ScrobbleProvider.AniList:
        return 'assets/images/ExternalServices/AniList.png';
      case ScrobbleProvider.Mal:
        return 'assets/images/ExternalServices/MAL.png';
      case ScrobbleProvider.GoogleBooks:
        return 'assets/images/ExternalServices/GoogleBooks.png';
      case ScrobbleProvider.Kavita:
        return 'assets/images/logo-32.png';
    }

    return '';
  }

}
