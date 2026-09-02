import {Component, inject, input, signal} from '@angular/core';
import {ChapterInfo} from "../../_models/chapter-info";
import {TranslocoDirective} from "@jsverse/transloco";
import {FormsModule, ReactiveFormsModule} from "@angular/forms";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ReviewService} from "../../../_services/review.service";
import {form, FormField, minLength} from "@angular/forms/signals";
import {NgxStarsModule} from "ngx-stars";
import {ThemeService} from "../../../_services/theme.service";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";

interface RateAndReviewForm {
  rating: number;
  review: string;
}


@Component({
  imports: [
    TranslocoDirective,
    FormsModule,
    ReactiveFormsModule,
    FormField,
    NgxStarsModule,
    FormFieldDirective,
    ValidationErrorsComponent
  ],
  selector: 'app-rate-and-review-series-modal',
  styleUrl: './rate-and-review-series-modal.component.scss',
  templateUrl: './rate-and-review-series-modal.component.html',
})
export class RateAndReviewSeriesModalComponent {

  // I should likely just pss the rating and review in directly + series title
  seriesId = input.required<number>();
  libraryId = input.required<number>();
  chapterInfo = input.required<ChapterInfo>();

  protected readonly modal = inject(NgbActiveModal);
  private readonly reviewService = inject(ReviewService);
  private readonly themeService = inject(ThemeService);

  private readonly formModel = signal<RateAndReviewForm>({
    rating: 0,
    review: ''
  });
  reviewForm = form(this.formModel, (schemaPath) => {
    minLength(schemaPath.review, 5)
  });
  starColor = this.themeService.getCssVariable('--rating-star-color');
  hasRated = false;

  ngOnInit() {
    this.reviewService.getMyRatingAndReview(this.seriesId()).subscribe(res => {
      this.reviewForm.rating().value.set(res.rating);
      this.reviewForm.review().value.set(res.review ?? '');
      this.hasRated = res.hasRated;
    });
  }

  updateRating(rating: number) {
    this.reviewService.updateRating(this.seriesId(), rating).subscribe(() => {
      this.reviewForm.rating().value.set(rating);
    });
  }


  close() {
    this.modal.dismiss();
  }

  save() {
    this.reviewService.updateReview(this.seriesId(), this.reviewForm.review().value() ?? '').subscribe(() => {
      this.close();
    });
  }
}
