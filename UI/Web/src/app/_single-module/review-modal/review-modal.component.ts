import {ChangeDetectionStrategy, Component, inject, input, OnInit, signal} from '@angular/core';
import {ReactiveFormsModule} from '@angular/forms';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {UserReview} from "../../_models/user-review";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ConfirmService} from "../../shared/confirm.service";
import {ToastrService} from '@openng/ngx-toastr';
import {NgxStarsModule} from "ngx-stars";
import {ReviewService} from "../../_services/review.service";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {form, FormField, minLength, required} from "@angular/forms/signals";
import {ValidationErrorsComponent} from "../../shared/_components/validation-errors/validation-errors.component";

export enum ReviewModalCloseAction {
  Create,
  Edit,
  Delete
}
export interface ReviewModalCloseEvent {
  success: boolean,
  review: UserReview;
  action: ReviewModalCloseAction
}

interface ReviewFormModel {
  reviewBody: string;
}

@Component({
  selector: 'app-review-series-modal',
  imports: [ReactiveFormsModule, TranslocoDirective, NgxStarsModule, FormFieldDirective, ValidationErrorsComponent, FormField],
  templateUrl: './review-modal.component.html',
  styleUrls: ['./review-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewModalComponent implements OnInit {

  protected readonly modal = inject(NgbActiveModal);
  private readonly reviewService = inject(ReviewService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastr = inject(ToastrService);
  protected readonly minLength = 5;

  review = input.required<UserReview>();
  private readonly reviewFormModel = signal<ReviewFormModel>({
    reviewBody: ''
  });
  reviewForm = form(this.reviewFormModel, (schemaPath) => {
    required(schemaPath.reviewBody);
    minLength(schemaPath.reviewBody, this.minLength);
  });

  ngOnInit(): void {
    this.reviewForm.reviewBody().value.set(this.review().body);
  }

  close() {
    this.modal.dismiss();
  }

  async delete() {
    if (!await this.confirmService.confirm(translate('toasts.delete-review'))) return;

    this.reviewService.deleteReview(this.review().seriesId, this.review().chapterId).subscribe(() => {
      this.toastr.success(translate('toasts.review-deleted'));
      this.modal.close({success: true, review: this.review, action: ReviewModalCloseAction.Delete});
    });

  }
  save() {
    const model = this.reviewFormModel();
    if (model.reviewBody.length < this.minLength) {
      return;
    }

    this.reviewService.updateReview(this.review().seriesId, model.reviewBody, this.review().chapterId).subscribe(review => {
      this.modal.close({success: true, review: review, action: ReviewModalCloseAction.Edit});
    });

  }
}
