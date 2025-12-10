import {ChangeDetectionStrategy, Component, computed, effect, inject, input, model} from '@angular/core';
import {AgeRating} from "../../_models/metadata/age-rating";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {FilterUtilitiesService} from "../../shared/_services/filter-utilities.service";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {FilterField} from "../../_models/metadata/v2/filter-field";

const basePath = './assets/images/ratings/';

@Component({
  selector: 'app-age-rating-image',
  imports: [
      ImageComponent,
      NgbTooltip,
      AgeRatingPipe,
  ],
  standalone: true,
  templateUrl: './age-rating-image.component.html',
  styleUrl: './age-rating-image.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgeRatingImageComponent {
  private readonly filterUtilityService = inject(FilterUtilitiesService);

  rating = input.required<AgeRating>();
  imageUrl = computed(() => this.computeImageUrl());

  computeImageUrl() {
    switch (this.rating()) {
      case AgeRating.Unknown:
        return basePath + 'unknown-rating.png';
      case AgeRating.RatingPending:
        return basePath + 'rating-pending-rating.png';
      case AgeRating.EarlyChildhood:
        return basePath + 'early-childhood-rating.png';
      case AgeRating.Everyone:
        return basePath + 'everyone-rating.png';
      case AgeRating.G:
        return basePath + 'g-rating.png';
      case AgeRating.Everyone10Plus:
        return basePath + 'everyone-10+-rating.png';
      case AgeRating.PG:
        return basePath + 'pg-rating.png';
      case AgeRating.KidsToAdults:
        return basePath + 'kids-to-adults-rating.png';
      case AgeRating.Teen:
        return basePath + 'teen-rating.png';
      case AgeRating.Mature15Plus:
        return basePath + 'ma15+-rating.png';
      case AgeRating.Mature17Plus:
        return basePath + 'mature-17+-rating.png';
      case AgeRating.Mature:
        return basePath + 'm-rating.png';
      case AgeRating.R18Plus:
        return basePath + 'r18+-rating.png';
      case AgeRating.AdultsOnly:
        return basePath + 'adults-only-18+-rating.png';
      case AgeRating.X18Plus:
        return basePath + 'x18+-rating.png';
    }

    return basePath + 'unknown-rating.png';
  }

  openRating() {
    this.filterUtilityService.applyFilter(['all-series'], FilterField.AgeRating, FilterComparison.Equal, `${this.rating}`).subscribe();
  }


}
