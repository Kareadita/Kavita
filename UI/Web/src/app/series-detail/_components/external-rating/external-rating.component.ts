import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  Input,
  OnInit,
  ViewEncapsulation
} from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
import {SeriesService} from "../../../_services/series.service";
import {Rating} from "../../../_models/rating";
import {ProviderImagePipe} from "../../../pipe/provider-image.pipe";
import {NgbPopover, NgbRating} from "@ng-bootstrap/ng-bootstrap";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {AccountService} from "../../../_services/account.service";
import {LibraryType} from "../../../_models/library";
import {ProviderNamePipe} from "../../../pipe/provider-name.pipe";

@Component({
  selector: 'app-external-rating',
  standalone: true,
  imports: [CommonModule, ProviderImagePipe, NgOptimizedImage, NgbRating, NgbPopover, LoadingComponent, ProviderNamePipe],
  templateUrl: './external-rating.component.html',
  styleUrls: ['./external-rating.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None
})
export class ExternalRatingComponent implements OnInit {
  @Input({required: true}) seriesId!: number;
  @Input({required: true}) userRating!: number;
  @Input({required: true}) libraryType!: LibraryType;
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly seriesService = inject(SeriesService);
  private readonly accountService = inject(AccountService);

  ratings: Array<Rating> = [];
  isLoading: boolean = false;
  overallRating: number = -1;


  ngOnInit() {

    this.seriesService.getOverallRating(this.seriesId).subscribe(r => this.overallRating = r.averageScore);

    this.accountService.hasValidLicense$.subscribe((res) => {
      if (!res) return;
      this.isLoading = true;
      this.cdRef.markForCheck();
      this.seriesService.getRatings(this.seriesId).subscribe(res => {
        this.ratings = res;
        this.isLoading = false;
        this.cdRef.markForCheck();
      }, () => {
        this.isLoading = false;
        this.cdRef.markForCheck();
      });
    });
  }

  updateRating(rating: any) {
    this.seriesService.updateRating(this.seriesId, rating).subscribe(() => {
      this.userRating = rating;
    });
  }
}
