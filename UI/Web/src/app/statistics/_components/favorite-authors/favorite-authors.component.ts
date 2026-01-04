import {ChangeDetectionStrategy, Component, computed, inject, input, ViewEncapsulation} from '@angular/core';
import {StatisticsService} from "../../../_services/statistics.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {ImageService} from "../../../_services/image.service";
import {ImageComponent} from "../../../shared/image/image.component";
import {TagBadgeComponent} from "../../../shared/tag-badge/tag-badge.component";
import {FormsModule, ReactiveFormsModule} from "@angular/forms";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {StatsFilter} from "../../_models/stats-filter";
import {RouterLink} from "@angular/router";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";

@Component({
  selector: 'app-favorite-authors',
  imports: [
    TranslocoDirective,
    LoadingComponent,
    ImageComponent,
    TagBadgeComponent,
    FormsModule,
    ReactiveFormsModule,
    NgbTooltip,
    RouterLink,
    StatsNoDataComponent
  ],
  templateUrl: './favorite-authors.component.html',
  styleUrl: './favorite-authors.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class FavoriteAuthorsComponent {

  private readonly statsService = inject(StatisticsService);
  protected readonly imageService = inject(ImageService);

  userId = input.required<number>();
  userName = input.required<string>();
  statsFilter = input.required<StatsFilter>();

  mostReadAuthor = computed(() => {
    const authors = this.favoriteAuthors.value() || [];
    if (authors.length === 0) return null;

    return authors.reduce((max, author) =>
      author.totalChaptersRead > max.totalChaptersRead ? author : max
    );
  });

  mostReadCount = computed(() => {
    const author = this.mostReadAuthor();
    return author?.totalChaptersRead ?? 0;
  });

  protected readonly favoriteAuthors = this.statsService.getFavouriteAuthors(
    () => this.statsFilter(),
    () => this.userId(),
  );

}
