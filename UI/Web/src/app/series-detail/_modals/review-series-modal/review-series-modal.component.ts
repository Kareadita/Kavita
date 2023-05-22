import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Series } from 'src/app/_models/series';
import { SeriesService } from 'src/app/_services/series.service';

@Component({
  selector: 'app-review-series-modal',
  templateUrl: './review-series-modal.component.html',
  styleUrls: ['./review-series-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewSeriesModalComponent implements OnInit {

  @Input({required: true}) series!: Series;
  reviewGroup!: FormGroup;

  constructor(public modal: NgbActiveModal, private seriesService: SeriesService, private readonly cdRef: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.reviewGroup = new FormGroup({
      review: new FormControl(this.series.userReview, []),
      rating: new FormControl(this.series.userRating, [])
    });
    this.cdRef.markForCheck();
  }

  close() {
    this.modal.close({success: false, review: null});
  }

  clearRating() {
    this.reviewGroup.get('rating')?.setValue(0);
    this.cdRef.markForCheck();
  }

  save() {
    const model = this.reviewGroup.value;
    this.seriesService.updateRating(this.series?.id, model.rating, model.review).subscribe(() => {
      this.modal.close({success: true, review: model.review, rating: model.rating});
    });
  }
}
