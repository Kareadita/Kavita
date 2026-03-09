import {ChangeDetectionStrategy, Component, computed, inject, input, model} from '@angular/core';
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {ReviewCardComponent} from "../review-card/review-card.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {UserReview} from "../../_models/user-review";
import {
  ReviewModalCloseAction,
  ReviewModalCloseEvent,
  ReviewModalComponent
} from "../review-modal/review-modal.component";
import {Series} from "../../_models/series";
import {Chapter} from "../../_models/chapter";
import {ModalService} from "../../_services/modal.service";
import {AccountService} from "../../_services/account.service";

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
  private readonly modalService = inject(ModalService);

  userReviews = model.required<UserReview[]>();
  plusReviews = input.required<UserReview[]>();
  series = input.required<Series>();
  volumeId = input<number | undefined>(undefined);
  chapter = input<Chapter | undefined>(undefined);

  myReviews = computed(() => this.userReviews().filter(r => r.username === this.accountService.currentUser()!.username && !r.isExternal));

  openReviewModal() {
    const userReview = this.myReviews();

    const modalRef = this.modalService.open(ReviewModalComponent);

    if (userReview.length > 0) {
      modalRef.setInput('review', userReview[0]);
    } else {
      modalRef.setInput('review', {
        seriesId: this.series().id,
        volumeId: this.volumeId(),
        chapterId: this.chapter()?.id,
        tagline: '',
        body: ''
      });
    }

    modalRef.closed.subscribe((closeResult) => {
      this.updateOrDeleteReview(closeResult);
    });
  }

  updateOrDeleteReview(closeResult: ReviewModalCloseEvent) {
    const reviews = this.userReviews();
    const index = reviews.findIndex(r => r.username === closeResult.review!.username);

    if (closeResult.action === ReviewModalCloseAction.Edit) {
      if (index === -1) {
        this.userReviews.set([closeResult.review!, ...reviews]);
      } else {
        this.userReviews.set(reviews.map((r, i) => i === index ? closeResult.review! : r));
      }
      return;
    }

    if (closeResult.action === ReviewModalCloseAction.Delete) {
      this.userReviews.set(reviews.filter(r => r.username !== closeResult.review!.username));
    }
  }
}
