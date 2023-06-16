import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {NgbActiveModal, NgbRating} from '@ng-bootstrap/ng-bootstrap';
import { Series } from 'src/app/_models/series';
import { SeriesService } from 'src/app/_services/series.service';
import {UserReview} from "../review-card/user-review";
import {CommonModule} from "@angular/common";
import {PipeModule} from "../../pipe/pipe.module";
import {SpoilerComponent} from "../spoiler/spoiler.component";

@Component({
  selector: 'app-review-series-modal',
  standalone: true,
  imports: [CommonModule, NgbRating, ReactiveFormsModule, PipeModule],
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
    this.seriesService.updateReview(this.review.seriesId, model.tagline, model.reviewBody).subscribe((updatedReview) => {
      this.modal.close({success: true, review: updatedReview});
    });
  }
}
