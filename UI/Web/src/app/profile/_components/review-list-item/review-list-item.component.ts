import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {DatePipe} from "@angular/common";
import {ImageComponent} from "../../../shared/image/image.component";
import {ReadMoreComponent} from "../../../shared/read-more/read-more.component";
import {UserReviewExtended} from "../../../_models/user-review";
import {ImageService} from "../../../_services/image.service";
import {CardActionablesComponent} from "../../../_single-module/card-actionables/card-actionables.component";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {NgxStarsModule} from "ngx-stars";
import {ThemeService} from "../../../_services/theme.service";

@Component({
  selector: 'app-review-list-item',
  imports: [
    TranslocoDirective,
    DatePipe,
    ImageComponent,
    ReadMoreComponent,
    NgbTooltip,
    NgxStarsModule
  ],
  templateUrl: './review-list-item.component.html',
  styleUrl: './review-list-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReviewListItemComponent {

  protected readonly imageService = inject(ImageService);
  protected readonly themeService = inject(ThemeService);

  review = input.required<UserReviewExtended>();

  imageUrl = computed(() => {
    const item = this.review();
    if (item.chapterId) {
      return this.imageService.getChapterCoverImage(item.chapterId);
    }

    return this.imageService.getSeriesCoverImage(item.seriesId);
  });

  title = computed(() => {
    const item = this.review();
    if (item.chapterId) {
      //if ( === LooseLeafOrDefaultNumber)

      // If there is no actual title name, use a Chapter/Volume formatting
      if (item.chapter?.titleName === item.series.name) {

      }

      return item.chapter?.titleName || item.series.name;
    }

    return item.series.name;
  });

  protected readonly starColor = this.themeService.getCssVariable('--rating-star-color');

}
