import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject, input,
  Input, model,
  OnInit, signal,
  ViewEncapsulation
} from '@angular/core';
import {Rating, RatingAuthority} from "../../../_models/rating";
import {ProviderImagePipe} from "../../../_pipes/provider-image.pipe";
import {NgbPopover} from "@ng-bootstrap/ng-bootstrap";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {LibraryType} from "../../../_models/library/library";
import {NgxStarsModule} from "ngx-stars";
import {ThemeService} from "../../../_services/theme.service";
import {ImageComponent} from "../../../shared/image/image.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {ImageService} from "../../../_services/image.service";
import {NgOptimizedImage, NgTemplateOutlet} from "@angular/common";
import {RatingModalComponent} from "../rating-modal/rating-modal.component";
import {ScrobbleProviderNamePipe} from "../../../_pipes/scrobble-provider-name.pipe";
import {ReviewService} from "../../../_services/review.service";
import {BreakpointService} from "../../../_services/breakpoint.service";
import {ModalService} from "../../../_services/modal.service";

@Component({
  selector: 'app-external-rating',
  imports: [ProviderImagePipe, NgbPopover, LoadingComponent, NgxStarsModule, ImageComponent,
    TranslocoDirective, SafeHtmlPipe, NgOptimizedImage, NgTemplateOutlet, ScrobbleProviderNamePipe],
  templateUrl: './external-rating.component.html',
  styleUrls: ['./external-rating.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None
})
export class ExternalRatingComponent implements OnInit {

  private readonly reviewService = inject(ReviewService);
  private readonly themeService = inject(ThemeService);
  protected readonly destroyRef = inject(DestroyRef);
  protected readonly imageService = inject(ImageService);
  protected readonly modalService = inject(ModalService);
  protected readonly breakpointService = inject(BreakpointService);


  seriesId = input.required<number>();
  libraryType = input.required<LibraryType>();
  ratings = input.required<Rating[]>();
  webLinks = input<string[]>([]);

  userRating = model.required<number>();
  hasUserRated = model.required<boolean>();

  chapterId = input<number | undefined>(undefined);

  isLoading = signal(false);
  overallRating = signal(-1);
  starColor = this.themeService.getCssVariable('--rating-star-color');

  ngOnInit() {
    this.reviewService.overallRating(this.seriesId(), this.chapterId()).subscribe(r => {
        this.overallRating.set(r.averageScore);
    });
  }

  updateRating(rating: number) {
    this.reviewService.updateRating(this.seriesId(), rating, this.chapterId()).subscribe(() => {
      this.userRating.set(rating);
      this.hasUserRated.set(true);
    });
  }

  openRatingModal() {
    const modalRef = this.modalService.open(RatingModalComponent);
    modalRef.setInput('userRating', this.userRating());
    modalRef.setInput('seriesId', this.seriesId());
    modalRef.setInput('hasUserRated', this.hasUserRated());
    modalRef.setInput('chapterId', this.chapterId());

    modalRef.closed.subscribe((updated: {hasUserRated: boolean, userRating: number}) => {
      this.userRating.set(updated.userRating);
      this.hasUserRated.update(x => x || updated.hasUserRated);
    });
  }

  getAuthorityTitle(rating: Rating) {
    if (rating.authority === RatingAuthority.Critic) {
      return ` (${translate('external-rating.critic')})`;
    }

    return '';
  }
}
