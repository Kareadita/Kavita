import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  inject,
  Input,
  OnInit,
  ViewEncapsulation
} from '@angular/core';
import {SeriesService} from "../../../_services/series.service";
import {Rating} from "../../../_models/rating";
import {ProviderImagePipe} from "../../../_pipes/provider-image.pipe";
import {NgbModal, NgbPopover} from "@ng-bootstrap/ng-bootstrap";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {LibraryType} from "../../../_models/library/library";
import {ProviderNamePipe} from "../../../_pipes/provider-name.pipe";
import {NgxStarsModule} from "ngx-stars";
import {ThemeService} from "../../../_services/theme.service";
import {Breakpoint, UtilityService} from "../../../shared/_services/utility.service";
import {ImageComponent} from "../../../shared/image/image.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {ImageService} from "../../../_services/image.service";
import {AsyncPipe, NgOptimizedImage, NgTemplateOutlet} from "@angular/common";
import {RatingModalComponent} from "../rating-modal/rating-modal.component";

@Component({
  selector: 'app-external-rating',
  standalone: true,
  imports: [ProviderImagePipe, NgbPopover, LoadingComponent, ProviderNamePipe, NgxStarsModule, ImageComponent,
    TranslocoDirective, SafeHtmlPipe, NgOptimizedImage, AsyncPipe, NgTemplateOutlet],
  templateUrl: './external-rating.component.html',
  styleUrls: ['./external-rating.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None
})
export class ExternalRatingComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly seriesService = inject(SeriesService);
  private readonly themeService = inject(ThemeService);
  public readonly utilityService = inject(UtilityService);
  public readonly destroyRef = inject(DestroyRef);
  public readonly imageService = inject(ImageService);
  public readonly modalService = inject(NgbModal);

  protected readonly Breakpoint = Breakpoint;

  @Input({required: true}) seriesId!: number;
  @Input({required: true}) userRating!: number;
  @Input({required: true}) hasUserRated!: boolean;
  @Input({required: true}) libraryType!: LibraryType;
  @Input({required: true}) ratings: Array<Rating> = [];
  @Input() webLinks: Array<string> = [];

  isLoading: boolean = false;
  overallRating: number = -1;
  starColor = this.themeService.getCssVariable('--rating-star-color');

  ngOnInit() {
    this.seriesService.getOverallRating(this.seriesId).subscribe(r => this.overallRating = r.averageScore);
  }

  updateRating(rating: number) {
    this.seriesService.updateRating(this.seriesId, rating).subscribe(() => {
      this.userRating = rating;
      this.hasUserRated = true;
      this.cdRef.markForCheck();
    });
  }

  openRatingModal() {
    const modalRef = this.modalService.open(RatingModalComponent, {size: 'xl'});
    modalRef.componentInstance.userRating = this.userRating;
    modalRef.componentInstance.seriesId = this.seriesId;
    modalRef.componentInstance.hasUserRated = this.hasUserRated;

    modalRef.closed.subscribe((updated: {hasUserRated: boolean, userRating: number}) => {
      this.userRating = updated.userRating;
      this.hasUserRated = this.hasUserRated || updated.hasUserRated;
      this.cdRef.markForCheck();
    });
  }
}
