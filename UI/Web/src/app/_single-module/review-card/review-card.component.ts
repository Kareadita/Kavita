import {ChangeDetectionStrategy, Component, Input} from '@angular/core';
import {CommonModule} from '@angular/common';
import {SharedModule} from "../../shared/shared.module";
import {UserReview} from "./user-review";
import {PipeModule} from "../../pipe/pipe.module";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {ReviewCardModalComponent} from "../review-card-modal/review-card-modal.component";

@Component({
  selector: 'app-review-card',
  standalone: true,
  imports: [CommonModule, SharedModule, PipeModule],
  templateUrl: './review-card.component.html',
  styleUrls: ['./review-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewCardComponent {

  @Input({required: true}) review!: UserReview;

  constructor(private readonly modalService: NgbModal) {
  }

  showModal() {
    const ref = this.modalService.open(ReviewCardModalComponent);
    ref.componentInstance.review = this.review;
  }

}
