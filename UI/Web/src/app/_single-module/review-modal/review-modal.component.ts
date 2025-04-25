import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {SeriesService} from 'src/app/_services/series.service';
import {UserReview} from "../review-card/user-review";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ConfirmService} from "../../shared/confirm.service";
import {ToastrService} from "ngx-toastr";
import {ChapterService} from "../../_services/chapter.service";
import {of} from "rxjs";

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
  imports: [ReactiveFormsModule, TranslocoDirective],
  templateUrl: './review-modal.component.html',
  styleUrls: ['./review-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewModalComponent implements OnInit {

  protected readonly modal = inject(NgbActiveModal);
  private readonly seriesService = inject(SeriesService);
  private readonly chapterService = inject(ChapterService);
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

    let obs;
    if (!this.review.chapterId) {
      obs = this.seriesService.deleteReview(this.review.seriesId);
    } else {
      obs = this.chapterService.deleteChapterReview(this.review.chapterId)
    }

    obs?.subscribe(() => {
      this.toastr.success(translate('toasts.review-deleted'));
      this.modal.close({success: true, review: this.review, action: ReviewSeriesModalCloseAction.Delete});
    });

  }
  save() {
    const model = this.reviewGroup.value;
    if (model.reviewBody.length < this.minLength) {
      return;
    }

    let obs;
    if (!this.review.chapterId) {
      obs = this.seriesService.updateReview(this.review.seriesId, model.reviewBody);
    } else {
      obs = this.chapterService.updateChapterReview(this.review.seriesId, this.review.chapterId, model.reviewBody);
    }

    obs?.subscribe(review => {
      this.modal.close({success: true, review: review, action: ReviewSeriesModalCloseAction.Edit});
    });

  }
}
