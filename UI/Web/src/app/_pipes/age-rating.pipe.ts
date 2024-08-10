import {inject, Pipe, PipeTransform} from '@angular/core';
import { Observable, of } from 'rxjs';
import { AgeRating } from '../_models/metadata/age-rating';
import { AgeRatingDto } from '../_models/metadata/age-rating-dto';
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'ageRating',
  standalone: true
})
export class AgeRatingPipe implements PipeTransform {

  translocoService = inject(TranslocoService);

  transform(value: AgeRating | AgeRatingDto | undefined): Observable<string> {
    if (value === undefined || value === null) return of(this.translocoService.translate('age-rating-pipe.unknown') as string);

    if (value.hasOwnProperty('title')) {
      return of((value as AgeRatingDto).title);
    }

    switch (value) {
      case AgeRating.Unknown:
        return this.translocoService.translate('age-rating-pipe.unknown');
      case AgeRating.EarlyChildhood:
        return this.translocoService.translate('age-rating-pipe.early-childhood');
      case AgeRating.AdultsOnly:
        return this.translocoService.translate('age-rating-pipe.adults-only');
      case AgeRating.Everyone:
        return this.translocoService.translate('age-rating-pipe.everyone');
      case AgeRating.Everyone10Plus:
        return this.translocoService.translate('age-rating-pipe.everyone-10-plus');
      case AgeRating.G:
        return this.translocoService.translate('age-rating-pipe.g');
      case AgeRating.KidsToAdults:
        return this.translocoService.translate('age-rating-pipe.kids-to-adults');
      case AgeRating.Mature:
        return this.translocoService.translate('age-rating-pipe.mature');
      case AgeRating.Mature15Plus:
        return this.translocoService.translate('age-rating-pipe.ma15-plus');
      case AgeRating.Mature17Plus:
        return this.translocoService.translate('age-rating-pipe.mature-17-plus');
      case AgeRating.RatingPending:
        return this.translocoService.translate('age-rating-pipe.rating-pending');
      case AgeRating.Teen:
        return this.translocoService.translate('age-rating-pipe.teen');
      case AgeRating.X18Plus:
        return this.translocoService.translate('age-rating-pipe.x18-plus');
      case AgeRating.NotApplicable:
        return this.translocoService.translate('age-rating-pipe.not-applicable');
      case AgeRating.PG:
        return this.translocoService.translate('age-rating-pipe.pg');
      case AgeRating.R18Plus:
        return this.translocoService.translate('age-rating-pipe.r18-plus');
    }

    return of(this.translocoService.translate('age-rating-pipe.unknown') as string);
  }

}
