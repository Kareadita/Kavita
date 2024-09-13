import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  Input,
  OnInit
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbActiveModal, NgbRating} from '@ng-bootstrap/ng-bootstrap';
import { SeriesService } from 'src/app/_services/series.service';
import {UserReview} from "../review-card/user-review";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ConfirmService} from "../../shared/confirm.service";
import {ToastrService} from "ngx-toastr";

export enum ReviewSeriesModalCloseAction {
  Create,
  Edit,
  Delete,
  Close
}
export interface ReviewSeriesModalCloseEvent {
  success: boolean,
  review: UserReview;
  action: ReviewSeriesModalCloseAction
}

@Component({
  selector: 'app-review-series-modal',
  standalone: true,
  imports: [NgbRating, ReactiveFormsModule, TranslocoDirective],
  templateUrl: './review-series-modal.component.html',
  styleUrls: ['./review-series-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewSeriesModalComponent implements OnInit {

  protected readonly modal = inject(NgbActiveModal);
  private readonly seriesService = inject(SeriesService);
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
    this.modal.close({success: false, review: this.review, action: ReviewSeriesModalCloseAction.Close});
  }

  async delete() {
    if (!await this.confirmService.confirm(translate('toasts.delete-review'))) return;
    this.seriesService.deleteReview(this.review.seriesId).subscribe(() => {
      this.toastr.success(translate('toasts.review-deleted'));
      this.modal.close({success: true, review: this.review, action: ReviewSeriesModalCloseAction.Delete});
    });
  }
  save() {
    const model = this.reviewGroup.value;
    if (model.reviewBody.length < this.minLength) {
      return;
    }
    this.seriesService.updateReview(this.review.seriesId, model.reviewBody).subscribe(review => {
      this.modal.close({success: true, review: review, action: ReviewSeriesModalCloseAction.Edit});
    });
  }
}
