import { Pipe, PipeTransform } from '@angular/core';
import {ScrobbleProvider} from "../_services/scrobbling.service";

@Pipe({
  name: 'providerName',
  standalone: true
})
export class ProviderNamePipe implements PipeTransform {

  transform(value: ScrobbleProvider): string {
    switch (value) {
      case ScrobbleProvider.AniList:
        return 'AniList';
      case ScrobbleProvider.Mal:
        return 'MAL';
      case ScrobbleProvider.Kavita:
        return 'Kavita';
      case ScrobbleProvider.GoogleBooks:
        return 'Google Books';
    }
  }

}
