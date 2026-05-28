import {ChangeDetectionStrategy, Component, computed, input, output} from '@angular/core';
import {ExternalSeriesMatch} from "../../../_models/series-detail/external-series-match";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ImageComponent} from "../../image/image.component";
import {MediaFormatPillComponent} from "../media-format-pill/media-format-pill.component";
import {ScrobbleProviderTagBadgeComponent} from "../scrobble-provider-tag-badge/scrobble-provider-tag-badge.component";
import {MatchStatusDotComponent} from "../match-status-dot/match-status-dot.component";
import {ConfidenceChipComponent} from "../confidence-chip/confidence-chip.component";

@Component({
  selector: 'app-match-series-result-item',
  imports: [
    TranslocoDirective,
    NgbTooltip,
    ImageComponent,
    MediaFormatPillComponent,
    ScrobbleProviderTagBadgeComponent,
    MatchStatusDotComponent,
    ConfidenceChipComponent,
  ],
  templateUrl: './match-series-result-item.component.html',
  styleUrl: './match-series-result-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MatchSeriesResultItemComponent {
  item = input.required<ExternalSeriesMatch>();
  isSelected = input<boolean>(false);
  showSynonyms = input<boolean>(true);
  query = input<string>('');
  selected = output<ExternalSeriesMatch>();

  protected readonly ScrobbleProvider = ScrobbleProvider;

  protected matchedSynonyms = computed(() => {
    const q = this.query().trim().toLowerCase();
    if (!q || q.length < 2 || /^(anilist|mal|mangabaka|cbr|hardcover):/.test(q)) return [];
    return (this.item().series.synonyms ?? []).filter(s => s.toLowerCase().includes(q));
  });

  protected readonly displaySynonyms = computed(() =>
    this.matchedSynonyms().length > 0
      ? this.matchedSynonyms()
      : (this.item().series.synonyms ?? [])
  );

  protected startYear = computed(() =>
    this.item().series.startDate ? new Date(this.item().series.startDate!).getFullYear() : null
  );

  protected endYear = computed(() =>
    this.item().series.endDate ? new Date(this.item().series.endDate!).getFullYear() : null
  );

  protected firstAuthor = computed(() =>
    this.item().series.staff?.find(s => s.role === 'Author')?.name ?? null
  );

  protected pct = computed(() => Math.round(this.item().matchRating * 100));

  selectItem() {
    this.selected.emit(this.item());
  }
}
