import { Pipe, PipeTransform } from '@angular/core';
import { map, Observable } from 'rxjs';
import { MetadataService } from '../_services/metadata.service';

@Pipe({
  name: 'languageName'
})
export class LanguageNamePipe implements PipeTransform {

  constructor(private metadataService: MetadataService) {
  }

  transform(isoCode: string): Observable<string> {
    return this.metadataService.getAllValidLanguages().pipe(map(lang => {
      const l = lang.filter(l => l.isoCode === isoCode);
      if (l.length > 0) return l[0].title;
      return '';
    }));
  }

}
