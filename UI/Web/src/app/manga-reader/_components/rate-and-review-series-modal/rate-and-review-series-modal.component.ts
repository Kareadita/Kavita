import {Component, inject, input, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {FormsModule, ReactiveFormsModule} from "@angular/forms";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ReviewService} from "../../../_services/review.service";
import {form, FormField, minLength} from "@angular/forms/signals";
import {NgxStarsModule} from "ngx-stars";
import {ThemeService} from "../../../_services/theme.service";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {UserRatingAndReview} from "../../../_models/user-rating-and-review";

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

  seriesId = input.required<number>();
  ratingReview = input.required<UserRatingAndReview>();
  seriesName = input.required<string>();

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

  ngOnInit() {
    this.reviewForm.rating().value.set(this.ratingReview().rating);
    this.reviewForm.review().value.set(this.ratingReview().review ?? '');
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
