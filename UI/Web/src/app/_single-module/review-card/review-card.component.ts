import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {NgOptimizedImage} from '@angular/common';
import {UserReview} from "./user-review";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {ReviewCardModalComponent} from "../review-card-modal/review-card-modal.component";
import {AccountService} from "../../_services/account.service";
import {ReviewModalCloseEvent, ReviewModalComponent} from "../review-modal/review-modal.component";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {RatingAuthority} from "../../_models/rating";

@Component({
  selector: 'app-review-card',
  imports: [ReadMoreComponent, DefaultValuePipe, NgOptimizedImage, ProviderImagePipe, TranslocoDirective],
  templateUrl: './review-card.component.html',
  styleUrls: ['./review-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewCardComponent implements OnInit {
  private readonly modalService = inject(NgbModal);
  private readonly cdRef = inject(ChangeDetectorRef);

  private readonly accountService = inject(AccountService);
  protected readonly ScrobbleProvider = ScrobbleProvider;

  @Input({required: true}) review!: UserReview;
  @Output() refresh = new EventEmitter<ReviewModalCloseEvent>();

  isMyReview: boolean = false;

  ngOnInit() {
    this.accountService.currentUser$.subscribe(u => {
      if (u) {
        this.isMyReview = this.review.username === u.username && !this.review.isExternal;
        this.cdRef.markForCheck();
      }
    });
  }

  showModal() {
    let component;
    if (this.isMyReview) {
      component = ReviewModalComponent;
    } else {
      component = ReviewCardModalComponent;
    }
    const ref = this.modalService.open(component, {size: 'lg', fullscreen: 'md'});

    ref.componentInstance.review = this.review;
    ref.closed.subscribe((res: ReviewModalCloseEvent | undefined) => {
      if (res) {
        this.refresh.emit(res);
      }
    })
  }

  protected readonly RatingAuthority = RatingAuthority;
}
