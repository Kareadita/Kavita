import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {AgeRating} from "../../_models/metadata/age-rating";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {AsyncPipe} from "@angular/common";

const basePath = './assets/images/ratings/';

@Component({
  selector: 'app-age-rating-image',
  standalone: true,
  imports: [
    ImageComponent,
    NgbTooltip,
    AgeRatingPipe,
    AsyncPipe
  ],
  templateUrl: './age-rating-image.component.html',
  styleUrl: './age-rating-image.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AgeRatingImageComponent implements OnInit {
  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required: true}) rating: AgeRating = AgeRating.Unknown;
  protected readonly AgeRating = AgeRating;

  imageUrl: string = 'unknown-rating.png';

  ngOnInit() {
    switch (this.rating) {
      case AgeRating.Unknown:
        this.imageUrl = basePath + 'unknown-rating.png';
        break;
      case AgeRating.RatingPending:
        this.imageUrl = basePath + 'rating-pending-rating.png';
        break;
      case AgeRating.EarlyChildhood:
        this.imageUrl = basePath + 'early-childhood-rating.png';
        break;
      case AgeRating.Everyone:
        this.imageUrl = basePath + 'everyone-rating.png';
        break;
      case AgeRating.G:
        this.imageUrl = basePath + 'g-rating.png';
        break;
      case AgeRating.Everyone10Plus:
        this.imageUrl = basePath + 'everyone-10+-rating.png';
        break;
      case AgeRating.PG:
        this.imageUrl = basePath + 'pg-rating.png';
        break;
      case AgeRating.KidsToAdults:
        this.imageUrl = basePath + 'kids-to-adults-rating.png';
        break;
      case AgeRating.Teen:
        this.imageUrl = basePath + 'teen-rating.png';
        break;
      case AgeRating.Mature15Plus:
        this.imageUrl = basePath + 'ma15+-rating.png';
        break;
      case AgeRating.Mature17Plus:
        this.imageUrl = basePath + 'mature-17+-rating.png';
        break;
      case AgeRating.Mature:
        this.imageUrl = basePath + 'm-rating.png';
        break;
      case AgeRating.R18Plus:
        this.imageUrl = basePath + 'r18+-rating.png';
        break;
      case AgeRating.AdultsOnly:
        this.imageUrl = basePath + 'adults-only-18+-rating.png';
        break;
      case AgeRating.X18Plus:
        this.imageUrl = basePath + 'x18+-rating.png';
        break;
    }
    this.cdRef.markForCheck();
  }

}
