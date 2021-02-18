import { Component, Input, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Series } from 'src/app/_models/series';
import { SeriesService } from 'src/app/_services/series.service';

@Component({
  selector: 'app-review-series-modal',
  templateUrl: './review-series-modal.component.html',
  styleUrls: ['./review-series-modal.component.scss']
})
export class ReviewSeriesModalComponent implements OnInit {

  @Input() series!: Series;
  reviewGroup!: FormGroup;

  constructor(private modalService: NgbModal, public modal: NgbActiveModal, private seriesService: SeriesService) {}

  ngOnInit(): void {
    this.reviewGroup = new FormGroup({
      review: new FormControl(this.series.userReview, [Validators.required])
    });
  }

  close() {
    this.modal.close({success: false, review: null});
  }

  save() {
    const model = this.reviewGroup.value;
    this.seriesService.updateRating(this.series?.id, this.series?.userRating, model.review).subscribe(() => {
      this.modal.close({success: true, review: model.review});
    });
  }

}
