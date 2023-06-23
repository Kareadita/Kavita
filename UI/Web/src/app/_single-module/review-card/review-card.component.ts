import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {SharedModule} from "../../shared/shared.module";
import {UserReview} from "./user-review";
import {PipeModule} from "../../pipe/pipe.module";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {ReviewCardModalComponent} from "../review-card-modal/review-card-modal.component";
import {AccountService} from "../../_services/account.service";
import {ReviewSeriesModalComponent} from "../review-series-modal/review-series-modal.component";

@Component({
  selector: 'app-review-card',
  standalone: true,
  imports: [CommonModule, SharedModule, PipeModule],
  templateUrl: './review-card.component.html',
  styleUrls: ['./review-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewCardComponent implements OnInit {

  @Input({required: true}) review!: UserReview;
  private readonly accountService = inject(AccountService);
  isMyReview: boolean = false;

  constructor(private readonly modalService: NgbModal, private readonly cdRef: ChangeDetectorRef) {}

  ngOnInit() {
    this.accountService.currentUser$.subscribe(u => {
      if (u) {
        this.isMyReview = this.review.username === u.username;
        this.cdRef.markForCheck();
      }
    });
  }

  showModal() {
    let component;
    if (this.isMyReview) {
      component = ReviewSeriesModalComponent;
    } else {
      component = ReviewCardModalComponent;
    }
    const ref = this.modalService.open(component, {size: "lg"});
    ref.componentInstance.review = this.review;
  }

}
