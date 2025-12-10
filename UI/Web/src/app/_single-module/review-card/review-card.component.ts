import {ChangeDetectionStrategy, Component, computed, EventEmitter, inject, input, Output} from '@angular/core';
import {NgOptimizedImage} from '@angular/common';
import {UserReview} from "../../_models/user-review";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
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

@Component({
  selector: 'app-review-card',
  imports: [ReadMoreComponent, NgOptimizedImage, ProviderImagePipe, TranslocoDirective, ProfileIconComponent, RouterLink, DefaultValuePipe],
  templateUrl: './review-card.component.html',
  styleUrls: ['./review-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewCardComponent {
  private readonly modalService = inject(NgbModal);

  private readonly accountService = inject(AccountService);
  protected readonly ScrobbleProvider = ScrobbleProvider;

  review = input.required<UserReview>();
  @Output() refresh = new EventEmitter<ReviewModalCloseEvent>();

  isMyReview = computed(() =>
    this.review().username === this.accountService.currentUserSignal()?.username && !this.review().isExternal);

  showModal() {
    let component;
    if (this.isMyReview()) {
      component = ReviewModalComponent;
    } else {
      component = ReviewCardModalComponent;
    }
    const ref = this.modalService.open(component, {size: 'lg', fullscreen: 'md'});

    ref.componentInstance.review = this.review();
    ref.closed.subscribe((res: ReviewModalCloseEvent | undefined) => {
      if (res) {
        this.refresh.emit(res);
      }
    })
  }

  protected readonly RatingAuthority = RatingAuthority;
}
