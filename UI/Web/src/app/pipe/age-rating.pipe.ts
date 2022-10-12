import { Pipe, PipeTransform } from '@angular/core';
import { Observable, of } from 'rxjs';
import { AgeRating } from '../_models/metadata/age-rating';
import { AgeRatingDto } from '../_models/metadata/age-rating-dto';
import { MetadataService } from '../_services/metadata.service';

@Pipe({
  name: 'ageRating'
})
export class AgeRatingPipe implements PipeTransform {

  constructor(private metadataService: MetadataService) {}

  transform(value: AgeRating | AgeRatingDto | undefined): Observable<string> {
    if (value === undefined || value === null) return of('undefined');

    if (value.hasOwnProperty('title')) {
      return of((value as AgeRatingDto).title);  
    }

    return this.metadataService.getAgeRating((value as AgeRating));
  }

}
