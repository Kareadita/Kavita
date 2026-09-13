import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  OnInit,
  signal,
  viewChild
} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgxStarsComponent, NgxStarsModule} from "ngx-stars";
import {ReviewListItemComponent} from "../review-list-item/review-list-item.component";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {ThemeService} from "../../../_services/theme.service";
import {MemberInfo} from "../../../_models/user/member-info";
import {ReviewService} from "../../../_services/review.service";
import {UserReviewExtended} from "../../../_models/user-review";
import {toObservable} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged, switchMap, tap} from "rxjs";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {form, FormField} from "@angular/forms/signals";

@Component({
  selector: 'app-profile-review-list',
  imports: [
    TranslocoDirective,
    NgxStarsModule,
    ReviewListItemComponent,
    VirtualScrollerModule,
    LoadingComponent,
    FormField
  ],
  templateUrl: './profile-review-list.component.html',
  styleUrl: './profile-review-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileReviewListComponent implements OnInit {

  private readonly themeService = inject(ThemeService);
  private readonly reviewService = inject(ReviewService);

  readonly starsComponent = viewChild.required(NgxStarsComponent);

  memberInfo = input.required<MemberInfo>();

  reviews = signal<UserReviewExtended[]>([]);
  isLoading = signal<boolean>(true);

  starColor = this.themeService.getCssVariable('--rating-star-color');
  formModel = signal({
    query: '',
    rating: 0
  });
  formGroup = form(this.formModel);

  trackByReview = (_index: number, review: UserReviewExtended): string => {
    return `${review.id}-${review.seriesId}-${review.chapterId || review.createdUtc}`;
  };

  compareReviews = (item1: UserReviewExtended, item2: UserReviewExtended): boolean => {
    if (!item1 && !item2) return true;
    if (!item1 || !item2) return false;

    // Use the trackBy function to get the identity and compare
    return this.trackByReview(0, item1) === this.trackByReview(0, item2);
  };

  constructor() {
    toObservable(this.formModel).pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(v => this.reviewService.getReviewsByUser(this.memberInfo().id, v.query?.trim() ?? null, v.rating ?? null)),
      tap(reviews => this.reviews.set(reviews)),
    ).subscribe();
  }

  ngOnInit() {
    this.reviewService.getReviewsByUser(this.memberInfo().id, null, null).pipe(
      tap(_ => this.isLoading.set(true)),
      tap(reviews => this.reviews.set(reviews)),
      tap(_ => this.isLoading.set(false)),
    ).subscribe();
  }

  updateRating(rating: number) {
    this.formGroup.rating().value.set(rating);
  }

  resetRating() {
    this.starsComponent().setRating(0);
    this.updateRating(0);
  }
}
