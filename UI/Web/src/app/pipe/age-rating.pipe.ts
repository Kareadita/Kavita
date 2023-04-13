import { Pipe, PipeTransform } from '@angular/core';
import { Observable, of } from 'rxjs';
import { AgeRating } from '../_models/metadata/age-rating';
import { AgeRatingDto } from '../_models/metadata/age-rating-dto';

@Pipe({
  name: 'ageRating'
})
export class AgeRatingPipe implements PipeTransform {

  constructor() {}

  transform(value: AgeRating | AgeRatingDto | undefined): Observable<string> {
    if (value === undefined || value === null) return of('Unknown');

    if (value.hasOwnProperty('title')) {
      return of((value as AgeRatingDto).title);  
    }

    switch(value) {
      case AgeRating.Unknown: return of('Unknown');
      case AgeRating.EarlyChildhood: return of('Early Childhood');
      case AgeRating.AdultsOnly: return of('Adults Only 18+');
      case AgeRating.Everyone: return of('Everyone');
      case AgeRating.Everyone10Plus: return of('Everyone 10+');
      case AgeRating.G: return of('G');
      case AgeRating.KidsToAdults: return of('Kids to Adults');
      case AgeRating.Mature: return of('Mature');
      case AgeRating.Mature17Plus: return of('M');
      case AgeRating.RatingPending: return of('Rating Pending');
      case AgeRating.Teen: return of('Teen');
      case AgeRating.X18Plus: return of('X18+');
      case AgeRating.NotApplicable: return of('Not Applicable');
    }

    return of('Unknown');
  }

}
