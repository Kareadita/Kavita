import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
import {UserReview} from "./user-review";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {ReviewCardModalComponent} from "../review-card-modal/review-card-modal.component";
import {AccountService} from "../../_services/account.service";
import {ReviewSeriesModalComponent} from "../review-series-modal/review-series-modal.component";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {DefaultValuePipe} from "../../pipe/default-value.pipe";
import {ImageComponent} from "../../shared/image/image.component";
import {ProviderImagePipe} from "../../pipe/provider-image.pipe";

@Component({
  selector: 'app-review-card',
  standalone: true,
  imports: [CommonModule, ReadMoreComponent, DefaultValuePipe, ImageComponent, NgOptimizedImage, ProviderImagePipe],
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
