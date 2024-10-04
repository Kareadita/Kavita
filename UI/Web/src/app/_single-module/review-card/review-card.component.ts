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
import {
  ReviewSeriesModalCloseEvent,
  ReviewSeriesModalComponent
} from "../review-series-modal/review-series-modal.component";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {ImageComponent} from "../../shared/image/image.component";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProvider} from "../../_services/scrobbling.service";

@Component({
  selector: 'app-review-card',
  standalone: true,
  imports: [ReadMoreComponent, DefaultValuePipe, ImageComponent, NgOptimizedImage, ProviderImagePipe, TranslocoDirective],
  templateUrl: './review-card.component.html',
  styleUrls: ['./review-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewCardComponent implements OnInit {
  private readonly accountService = inject(AccountService);
  protected readonly ScrobbleProvider = ScrobbleProvider;

  @Input({required: true}) review!: UserReview;
  @Output() refresh = new EventEmitter<ReviewSeriesModalCloseEvent>();

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
    const ref = this.modalService.open(component, {size: 'lg', fullscreen: 'md'});
    ref.componentInstance.review = this.review;
    ref.closed.subscribe((res: ReviewSeriesModalCloseEvent | undefined) => {
      if (res) {
        this.refresh.emit(res);
      }
    })
  }
}
