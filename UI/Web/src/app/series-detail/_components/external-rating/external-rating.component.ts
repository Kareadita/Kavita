import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
import {SeriesService} from "../../../_services/series.service";
import {Rating} from "../../../_models/rating";
import {ProviderImagePipe} from "../../../pipe/provider-image.pipe";
import {NgbRating} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-external-rating',
  standalone: true,
  imports: [CommonModule, ProviderImagePipe, NgOptimizedImage, NgbRating],
  templateUrl: './external-rating.component.html',
  styleUrls: ['./external-rating.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExternalRatingComponent implements OnInit {
  @Input({required: true}) seriesId!: number;
  @Input({required: true}) userRating!: number;
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly seriesService = inject(SeriesService);

  ratings: Array<Rating> = [];


  ngOnInit() {
    this.seriesService.getRatings(this.seriesId).subscribe(res => {
      this.ratings = res;
      this.cdRef.markForCheck();
    })
  }

  updateRating(rating: any) {
    this.seriesService.updateRating(this.seriesId, rating).subscribe(() => {
      this.userRating = rating;
    });
  }
}
