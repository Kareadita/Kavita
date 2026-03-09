import {ChangeDetectionStrategy, Component, computed, inject, input, output} from '@angular/core';
import {NgOptimizedImage} from '@angular/common';
import {UserReview} from "../../_models/user-review";
import {ReviewCardModalComponent} from "../review-card-modal/review-card-modal.component";
import {AccountService} from "../../_services/account.service";
import {ReviewModalCloseEvent, ReviewModalComponent} from "../review-modal/review-modal.component";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {RatingAuthority} from "../../_models/rating";
import {ProfileIconComponent} from "../profile-icon/profile-icon.component";
import {RouterLink} from "@angular/router";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {ModalService} from "../../_services/modal.service";

@Component({
  selector: 'app-review-card',
  imports: [ReadMoreComponent, NgOptimizedImage, ProviderImagePipe, TranslocoDirective, ProfileIconComponent, RouterLink, DefaultValuePipe],
  templateUrl: './review-card.component.html',
  styleUrls: ['./review-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewCardComponent {
  private readonly modalService = inject(ModalService);
  private readonly accountService = inject(AccountService);


  review = input.required<UserReview>();
  readonly refresh = output<ReviewModalCloseEvent>();

  isMyReview = computed(() =>
    this.review().username === this.accountService.currentUser()?.username && !this.review().isExternal);

  showModal() {
    let ref;
    if (this.isMyReview()) {
      ref = this.modalService.open(ReviewModalComponent);
    } else {
      ref = this.modalService.open(ReviewCardModalComponent);
    }

    ref.setInput('review', this.review());
    ref.closed.subscribe((res: ReviewModalCloseEvent | undefined) => {
      if (res) {
        this.refresh.emit(res);
      }
    })
  }

  protected readonly RatingAuthority = RatingAuthority;
  protected readonly ScrobbleProvider = ScrobbleProvider;
}
