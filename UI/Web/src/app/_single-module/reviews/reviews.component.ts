import {ChangeDetectionStrategy, ChangeDetectorRef, Component, Inject, Input, OnInit} from '@angular/core';
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {ReviewCardComponent} from "../review-card/review-card.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {UserReview} from "../review-card/user-review";
import {User} from "../../_models/user";
import {AccountService} from "../../_services/account.service";
import {
  ReviewModalComponent, ReviewSeriesModalCloseAction,
  ReviewSeriesModalCloseEvent
} from "../review-modal/review-modal.component";
import {DefaultModalOptions} from "../../_models/default-modal-options";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {Series} from "../../_models/series";
import {Volume} from "../../_models/volume";
import {Chapter} from "../../_models/chapter";

@Component({
  selector: 'app-reviews',
  imports: [
    CarouselReelComponent,
    ReviewCardComponent,
    TranslocoDirective
  ],
  templateUrl: './reviews.component.html',
  styleUrl: './reviews.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewsComponent {

  @Input({required: true}) userReviews!: Array<UserReview>;
  @Input({required: true}) plusReviews!: Array<UserReview>;
  @Input({required: true}) series!: Series;
  @Input() volumeId: number | undefined;
  @Input() chapter: Chapter | undefined;

  @Input() reviewLocation: 'series' | 'chapter' = 'series';

  user: User | undefined;

  constructor(
    private accountService: AccountService,
    private modalService: NgbModal,
    private cdRef: ChangeDetectorRef) {

    this.accountService.currentUser$.subscribe(user => {
      if (user) {
        this.user = user;
      }
    });
  }

  iconClasses(): string {
    let classes = 'fa-solid';
    if (this.canEditOrAdd()) {
      classes += 'fa-' + (this.getUserReviews().length > 0 ? 'pen' : 'plus');
    }
    return classes;
  }

  canEditOrAdd(): boolean {
    if (this.reviewLocation === 'series') {
      return true;
    }

    if (this.reviewLocation === 'chapter') {
      return this.chapter !== undefined;
    }

    return false;
  }

  openReviewModal() {
    const userReview = this.getUserReviews();

    const modalRef = this.modalService.open(ReviewModalComponent, DefaultModalOptions);
    modalRef.componentInstance.reviewLocation = this.reviewLocation;

    if (userReview.length > 0) {
      modalRef.componentInstance.review = userReview[0];
    } else {
      modalRef.componentInstance.review = {
        seriesId: this.series.id,
        volumeId: this.volumeId,
        chapterId: this.chapter?.id,
        tagline: '',
        body: ''
      };
    }

    modalRef.closed.subscribe((closeResult) => {
      this.updateOrDeleteReview(closeResult);
    });

  }

  updateOrDeleteReview(closeResult: ReviewSeriesModalCloseEvent) {
    if (closeResult.action === ReviewSeriesModalCloseAction.Close) return;

    const index = this.userReviews.findIndex(r => r.username === closeResult.review!.username);
    if (closeResult.action === ReviewSeriesModalCloseAction.Edit) {
      if (index === -1 ) {
        this.userReviews = [closeResult.review, ...this.userReviews];
        this.cdRef.markForCheck();
        return;
      }
      this.userReviews[index] = closeResult.review;
      this.cdRef.markForCheck();
      return;
    }

    if (closeResult.action === ReviewSeriesModalCloseAction.Delete) {
      this.userReviews = [...this.userReviews.filter(r => r.username !== closeResult.review!.username)];
      this.cdRef.markForCheck();
      return;
    }
  }

  getUserReviews() {
    if (!this.user) {
      return [];
    }
    return this.userReviews.filter(r => r.username === this.user?.username && !r.isExternal);
  }

}
