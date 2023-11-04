import { Pipe, PipeTransform } from '@angular/core';
import { map, Observable } from 'rxjs';
import { MetadataService } from '../_services/metadata.service';
import {shareReplay} from "rxjs/operators";

@Pipe({
  name: 'languageName',
  standalone: true
})
export class LanguageNamePipe implements PipeTransform {

  constructor(private metadataService: MetadataService) {
  }

  transform(isoCode: string): Observable<string> {
    // TODO: See if we can speed this up. It rarely changes and is quite heavy to download on each page
    return this.metadataService.getAllValidLanguages().pipe(map(lang => {
      const l = lang.filter(l => l.isoCode === isoCode);
      if (l.length > 0) return l[0].title;
      return '';
    }), shareReplay());
  }

}
