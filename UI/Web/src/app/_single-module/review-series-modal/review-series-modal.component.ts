import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbActiveModal, NgbRating} from '@ng-bootstrap/ng-bootstrap';
import { SeriesService } from 'src/app/_services/series.service';
import {UserReview} from "../review-card/user-review";
import {CommonModule} from "@angular/common";
import {TranslocoDirective} from "@ngneat/transloco";

@Component({
  selector: 'app-review-series-modal',
  standalone: true,
    imports: [CommonModule, NgbRating, ReactiveFormsModule, TranslocoDirective],
  templateUrl: './review-series-modal.component.html',
  styleUrls: ['./review-series-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewSeriesModalComponent implements OnInit {

  @Input({required: true}) review!: UserReview;
  reviewGroup!: FormGroup;

  constructor(public modal: NgbActiveModal, private seriesService: SeriesService, private readonly cdRef: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.reviewGroup = new FormGroup({
      tagline: new FormControl(this.review.tagline || '', [Validators.min(20), Validators.max(120)]),
      reviewBody: new FormControl(this.review.body, [Validators.min(20)]),
    });
    this.cdRef.markForCheck();
  }

  close() {
    this.modal.close({success: false, review: null});
  }

  save() {
    const model = this.reviewGroup.value;
    this.seriesService.updateReview(this.review.seriesId, model.tagline, model.reviewBody).subscribe(() => {
      this.modal.close({success: true});
    });
  }
}
