import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  inject,
  Input,
  OnInit
} from '@angular/core';
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

  protected readonly modal = inject(NgbActiveModal);
  private readonly seriesService = inject(SeriesService);
  private readonly cdRef = inject(ChangeDetectorRef);
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
    this.modal.close({success: false, review: null});
  }

  save() {
    const model = this.reviewGroup.value;
    if (model.reviewBody.length < this.minLength) {
      return;
    }
    this.seriesService.updateReview(this.review.seriesId, model.reviewBody).subscribe(review => {
      this.modal.close({success: true, review: review});
    });
  }
}
