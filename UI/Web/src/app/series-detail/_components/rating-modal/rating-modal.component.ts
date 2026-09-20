import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, input, Input, model} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {NgxStarsModule} from "ngx-stars";
import {ThemeService} from "../../../_services/theme.service";
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

  userRating = model.required<number>();
  seriesId = input.required<number>();
  hasUserRated = model.required<boolean>();
  chapterId = input<number | undefined>(undefined);
  starColor = this.themeService.getCssVariable('--rating-star-color');


  updateRating(rating: number) {
    this.reviewService.updateRating(this.seriesId(), rating, this.chapterId()).subscribe(() => {
      this.userRating.set(rating);
      this.hasUserRated.set(true);
      this.close();
    });
  }

  close() {
    this.modal.close({hasUserRated: this.hasUserRated(), userRating: this.userRating()});
  }
}
