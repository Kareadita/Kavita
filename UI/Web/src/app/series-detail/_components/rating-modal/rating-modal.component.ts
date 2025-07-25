import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {Breakpoint} from "../../../shared/_services/utility.service";
import {NgxStarsModule} from "ngx-stars";
import {ThemeService} from "../../../_services/theme.service";
import {SeriesService} from "../../../_services/series.service";
import {ReviewService} from "../../../_services/review.service";

@Component({
    selector: 'app-rating-modal',
    imports: [
        TranslocoDirective,
        NgxStarsModule
    ],
    templateUrl: './rating-modal.component.html',
    styleUrl: './rating-modal.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class RatingModalComponent {

  protected readonly modal = inject(NgbActiveModal);
  protected readonly themeService = inject(ThemeService);
  protected readonly reviewService = inject(ReviewService);
  protected readonly cdRef = inject(ChangeDetectorRef);

  protected readonly Breakpoint = Breakpoint;

  @Input({required: true}) userRating!: number;
  @Input({required: true}) seriesId!: number;
  @Input({required: true}) hasUserRated!: boolean;
  @Input() chapterId: number | undefined;
  starColor = this.themeService.getCssVariable('--rating-star-color');


  updateRating(rating: number) {
    this.reviewService.updateRating(this.seriesId, rating, this.chapterId).subscribe(() => {
      this.userRating = rating;
      this.hasUserRated = true;
      this.cdRef.markForCheck();
      this.close();
    });
  }

  close() {
    this.modal.close({hasUserRated: this.hasUserRated, userRating: this.userRating});
  }
}
