import { Pipe, PipeTransform, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { MetadataService } from '../_services/metadata.service';
import {shareReplay} from "rxjs/operators";

@Pipe({
  name: 'languageName',
  standalone: true
})
export class LanguageNamePipe implements PipeTransform {
  private metadataService = inject(MetadataService);


  transform(isoCode: string): Observable<string> {
    return this.metadataService.getLanguageNameForCode(isoCode).pipe(shareReplay());
  }

}
