import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input} from '@angular/core';
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {ReviewCardComponent} from "../review-card/review-card.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {UserReview} from "../../_models/user-review";
import {User} from "../../_models/user/user";
import {AccountService} from "../../_services/account.service";
import {
  ReviewModalCloseAction,
  ReviewModalCloseEvent,
  ReviewModalComponent
} from "../review-modal/review-modal.component";
import {DefaultModalOptions} from "../../_models/default-modal-options";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {Series} from "../../_models/series";
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

  private readonly accountService = inject(AccountService);
  private readonly modalService = inject(NgbModal);
  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required: true}) userReviews!: Array<UserReview>;
  @Input({required: true}) plusReviews!: Array<UserReview>;
  @Input({required: true}) series!: Series;
  @Input() volumeId: number | undefined;
  @Input() chapter: Chapter | undefined;

  user: User | undefined = undefined;

  constructor() {
    this.accountService.currentUser$.subscribe(user => {
      if (user) {
        this.user = user;
      }
    });
  }

  openReviewModal() {
    const userReview = this.getUserReviews();

    const modalRef = this.modalService.open(ReviewModalComponent, DefaultModalOptions);

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

  updateOrDeleteReview(closeResult: ReviewModalCloseEvent) {
    if (closeResult.action === ReviewModalCloseAction.Close) return;

    const index = this.userReviews.findIndex(r => r.username === closeResult.review!.username);
    if (closeResult.action === ReviewModalCloseAction.Edit) {
      if (index === -1 ) {
        this.userReviews = [closeResult.review, ...this.userReviews];
        this.cdRef.markForCheck();
        return;
      }
      this.userReviews[index] = closeResult.review;
      this.cdRef.markForCheck();
      return;
    }

    if (closeResult.action === ReviewModalCloseAction.Delete) {
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
