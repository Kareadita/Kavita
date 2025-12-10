import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {UserReview} from "../../_models/user-review";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ConfirmService} from "../../shared/confirm.service";
import {ToastrService} from "ngx-toastr";
import {NgxStarsModule} from "ngx-stars";
import {ReviewService} from "../../_services/review.service";

export enum ReviewModalCloseAction {
  Create,
  Edit,
  Delete,
  Close
}
export interface ReviewModalCloseEvent {
  success: boolean,
  review: UserReview;
  action: ReviewModalCloseAction
}

@Component({
  selector: 'app-review-series-modal',
  imports: [ReactiveFormsModule, TranslocoDirective, NgxStarsModule],
  templateUrl: './review-modal.component.html',
  styleUrls: ['./review-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewModalComponent implements OnInit {

  protected readonly modal = inject(NgbActiveModal);
  private readonly reviewService = inject(ReviewService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastr = inject(ToastrService);
  protected readonly minLength = 5;

  @Input({required: true}) review!: UserReview;
  reviewGroup!: FormGroup;

  ngOnInit(): void {
    this.reviewGroup = new FormGroup({
      reviewBody: new FormControl(this.review.body, [Validators.required, Validators.minLength(this.minLength)]),
    });
    this.cdRef.markForCheck();
  }

  close() {
    this.modal.close({success: false, review: this.review, action: ReviewModalCloseAction.Close});
  }

  async delete() {
    if (!await this.confirmService.confirm(translate('toasts.delete-review'))) return;

    this.reviewService.deleteReview(this.review.seriesId, this.review.chapterId).subscribe(() => {
      this.toastr.success(translate('toasts.review-deleted'));
      this.modal.close({success: true, review: this.review, action: ReviewModalCloseAction.Delete});
    });

  }
  save() {
    const model = this.reviewGroup.value;
    if (model.reviewBody.length < this.minLength) {
      return;
    }

    this.reviewService.updateReview(this.review.seriesId, model.reviewBody, this.review.chapterId).subscribe(review => {
      this.modal.close({success: true, review: review, action: ReviewModalCloseAction.Edit});
    });

  }
}
