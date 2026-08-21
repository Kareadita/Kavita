import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  OnInit,
  signal,
  Signal
} from '@angular/core';
import {Series} from "../../_models/series";
import {SeriesService} from "../../_services/series.service";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {NgbActiveModal, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {ExternalSeriesMatch} from "../../_models/series-detail/external-series-match";
import {ToastrService} from "ngx-toastr";
import {catchError, filter, of, pairwise, skip, startWith, tap} from "rxjs";
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {EmptyStateComponent} from "../../shared/_components/empty-state/empty-state.component";
import {
  MatchSeriesResultItemComponent
} from "../../shared/_components/match-series-result-item/match-series-result-item.component";
import {ImageComponent} from "../../shared/image/image.component";
import {ImageService} from "../../_services/image.service";
import {SeriesDetail} from "../../_models/series-detail/series-detail";
import {
  ScrobbleProviderTagBadgeComponent
} from "../../shared/_components/scrobble-provider-tag-badge/scrobble-provider-tag-badge.component";
import {MatchSeriesInfo} from "../../_models/kavitaplus/match-series-info";
import {AllMetadataProviders, MetadataProvider} from "../../_models/kavitaplus/metadata-provider.enum";
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {MetadataProviderTitlePipe} from "../../_pipes/metadata-provider-title.pipe";
import {ExternalEditionDto, PlusMediaFormat} from "../../_models/series-detail/external-series-detail";
import {SeriesFormatComponent} from "../../shared/series-format/series-format.component";
import {LibraryTypePipe} from "../../_pipes/library-type.pipe";
import {SeriesMetadata} from "../../_models/metadata/series-metadata";

@Component({
  selector: 'app-match-series-modal',
  imports: [
    ReactiveFormsModule,
    TranslocoDirective,
    NgbTooltip,
    EmptyStateComponent,
    MatchSeriesResultItemComponent,
    ImageComponent,
    ScrobbleProviderTagBadgeComponent,
    MetadataProviderTitlePipe,
    SeriesFormatComponent,
    LibraryTypePipe,

  ],
  templateUrl: './match-series-modal.component.html',
  styleUrl: './match-series-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MatchSeriesModalComponent implements OnInit {

  private readonly seriesService = inject(SeriesService);
  private readonly modalService = inject(NgbActiveModal);
  private readonly toastr = inject(ToastrService);
  private readonly imageService = inject(ImageService);

  series = input.required<Series>();

  formGroup = new FormGroup({
    query: new FormControl('', []),
    isStandAlone: new FormControl(false),
    dontMatch: new FormControl(false, []),
    provider: new FormControl<MetadataProvider | null>(null),
  });

  protected readonly isDontMatch = toSignal(
    this.formGroup.controls.dontMatch.valueChanges.pipe(
      startWith(this.formGroup.controls.dontMatch.value)
    ),
    { initialValue: false }
  );

  canSaveDontMatch!: Signal<boolean>;
  matches = signal<ExternalSeriesMatch[]>([]);
  isLoading = signal<boolean>(false);
  hasSearched = signal<boolean>(false);
  selectedItem = signal<ExternalSeriesMatch | null>(null);
  selectedEdition = signal<ExternalEditionDto | null>(null);
  selectedEditionId = linkedSignal<string | null>(() => this.selectedEdition()?.id ?? null);
  lastQuery = signal<string>('');
  /** Provider the results on screen came from. */
  resultProvider = signal<MetadataProvider | null>(null);

  protected bodyState = computed<'empty' | 'dont-match' | 'loading' | 'results' | 'no-results'>(() => {
    if (this.isDontMatch()) return 'dont-match';
    if (this.isLoading()) return 'loading';
    if (!this.hasSearched()) return 'empty';
    return this.matches().length > 0 ? 'results' : 'no-results';
  });

  coverImageUrl!: Signal<string>;
  kavitaVolumeCount!: Signal<number>;
  kavitaChapterCount!: Signal<number>;
  kavitaSpecialCount!: Signal<number>;
  seriesDetail = signal<SeriesDetail | null>(null);
  seriesMetadata = signal<SeriesMetadata | null>(null);
  matchInfo = signal<MatchSeriesInfo | null>(null);

  peopleHint = computed(() => {
    const metadata = this.seriesMetadata();
    if (!metadata) return null;

    return metadata.writers
      .concat(metadata.colorists)
      .concat(metadata.coverArtists)
      .slice(0, 3)
      .map(person => person.name)
      .join(', ');
  });

  mangaBakaSearchUrl = computed(() => {
    return `https://mangabaka.org/search?q=${encodeURIComponent(this.series().name)}`;
  });

  hardcoverSearchUrl = computed(() => {
    const isStandalone = this.series().isStandAlone;
    const extraQuery = (isStandalone) ? '&type=Series' : '';
    return `https://hardcover.app/search?q=${encodeURIComponent(this.series().name)}${extraQuery}`;
  });

  cbrSearchUrl = computed(() => {
    return `https://comicbookroundup.com/search-results?keyword=${encodeURIComponent(this.series().name)}`;
  });

  /** Provider the results came from, falling back to the series' own before the first search lands */
  protected activeProvider = computed(() => this.resultProvider() ?? this.matchInfo()?.metadataProvider ?? null);
  /** The search ran against another provider than the series own, so applying a match will switch the Series over */
  protected isProviderSwitched = computed(() => {
    const provider = this.resultProvider();
    return provider !== null && provider !== this.matchInfo()?.metadataProvider;
  });


  constructor() {
    this.canSaveDontMatch = computed(() => this.isDontMatch() === true && !this.series().dontMatch);
    this.coverImageUrl = computed(() => this.imageService.getSeriesCoverImage(this.series().id));
    this.kavitaVolumeCount = computed(() => (this.seriesDetail()?.volumes ?? []).length);
    this.kavitaChapterCount = computed(() => (this.seriesDetail()?.chapters ?? []).length);
    this.kavitaSpecialCount = computed(() => (this.seriesDetail()?.specials ?? []).length);

    effect(() => {
      if (this.isDontMatch()) {
        this.formGroup.controls.query.disable({ emitEvent: false });
      } else {
        this.formGroup.controls.query.enable({ emitEvent: false });
      }
    });

    this.formGroup.controls.dontMatch.valueChanges.pipe(
      skip(1),
      filter(v => v === false),
      takeUntilDestroyed()
    ).subscribe(() => this.search());

    this.formGroup.valueChanges.pipe(
      takeUntilDestroyed(),
      pairwise(),
      filter(([prev, curr]) => prev.provider !== curr.provider),
    ).subscribe(() => {
      this.selectedItem.set(null);
      this.selectedEdition.set(null);
      this.search();
    });
  }

  ngOnInit() {
    if (this.series().mangaBakaEditionId) {
      this.selectedEditionId.set(this.series().mangaBakaEditionId);
    }

    this.seriesService.getMatchInfo(this.series().id).subscribe(res => {
      this.matchInfo.set(res);
      this.formGroup.controls.provider.setValue(res.metadataProvider, { emitEvent: false });
      this.autoSelectExistingMatch(this.matches());
    });

    this.formGroup.patchValue({ dontMatch: this.series().dontMatch || false });
    this.seriesService.getSeriesDetail(this.series().id).pipe(
      tap(detail => {
        this.seriesDetail.set(detail)

        const isStandAlone = detail.chapters.length + detail.specials.length == 1;
        this.formGroup.get('isStandAlone')?.setValue(isStandAlone);

        this.search();
      }),
    ).subscribe();

    this.seriesService.getMetadata(this.series().id).pipe(
      tap(metadata => this.seriesMetadata.set(metadata)),
    ).subscribe();
  }

  search() {
    if (this.isDontMatch()) return;

    const query = this.formGroup.value.query ?? '';

    this.isLoading.set(true);
    this.lastQuery.set(query);

    const model: any = { ...this.formGroup.value, seriesId: this.series().id };

    this.seriesService.matchSeries(model).pipe(
      tap(res => {
        this.isLoading.set(false);
        this.hasSearched.set(true);
        this.matches.set(res.matches);

        // The backend can search against another provider than the one asked for, when the query is a
        // provider specific url/header. Show what the results actually came from
        this.resultProvider.set(res.provider);
        this.formGroup.controls.provider.setValue(res.provider, { emitEvent: false });

        this.autoSelectExistingMatch(res.matches);
      }),
      catchError(() => {
        this.isLoading.set(false);
        this.hasSearched.set(true);
        return of(null);
      })
    ).subscribe();
  }

  private autoSelectExistingMatch(results: ExternalSeriesMatch[]) {
    if (this.selectedItem()) return;

    const info = this.matchInfo();
    if (!info || !info.hasMatch) return;

    const existing = results.find(r => this.isExistingMatch(r, info));
    if (!existing) return;

    this.selectedItem.set(existing);

    const editionId = info.mangaBakaEditionId || this.series().mangaBakaEditionId;
    if (editionId) {
      const edition = existing.series.editions.find(e => e.id === editionId) ?? null;
      this.selectedEdition.set(edition);
    }
  }

  private isExistingMatch(match: ExternalSeriesMatch, info: MatchSeriesInfo): boolean {
    const s = match.series;

    if (info.isLegacy) {
      return !!info.aniListId && s.aniListId === info.aniListId;
    }

    switch (info.metadataProvider) {
      case MetadataProvider.Mangabaka:
        return !!info.mangaBakaId && s.mangabakaId === info.mangaBakaId;
      case MetadataProvider.ComicBookRoundup:
        return !!info.cbrId && s.cbrId === info.cbrId;
      case MetadataProvider.Hardcover:
        return !!info.hardcoverId && s.hardcoverId === info.hardcoverId;
      default:
        return false;
    }
  }

  clearQuery() {
    this.formGroup.get('query')?.setValue('');
  }

  matchKey(item: ExternalSeriesMatch) {
    return `${item.series.provider}_${item.series.mangabakaId}_${item.series.hardcoverId}_${item.series.cbrId}_${item.series.aniListId}_${item.series.malId}`;
  }

  private findDefaultEdition(item: ExternalSeriesMatch): ExternalEditionDto | null {
    return item.series.editions?.find(e =>
      e.title?.trim().toLowerCase().endsWith('standard edition') &&
      e.format?.trim().toLowerCase() === 'digital') ?? null;
  }

  selectRow(item: ExternalSeriesMatch) {
    const current = this.selectedItem();
    if (current && this.matchKey(current) === this.matchKey(item)) return;

    this.selectedItem.set(item);
    this.selectedEdition.set(this.findDefaultEdition(item));
  }

  selectEdition(item: ExternalSeriesMatch, edition: ExternalEditionDto) {
    const current = this.selectedItem();
    const sameItem = !!current && this.matchKey(current) === this.matchKey(item);

    if (sameItem && this.selectedEdition()?.id === edition.id) {
      this.selectedEdition.set(null);
      return;
    }

    this.selectedItem.set(item);
    this.selectedEdition.set(edition);
  }

  close() {
    this.modalService.dismiss();
  }

  applyMatch() {
    const item = this.selectedItem();
    if (!item) return;

    const data = item.series;
    data.tags = data.tags || [];
    data.genres = data.genres || [];

    // Sending the provider the results came from lets the backend switch the Series over to it, if it differs
    this.seriesService.updateMatch(this.series().id, data, this.selectedEdition(), this.resultProvider()).subscribe(() => {
      this.modalService.close(true);
    });
  }

  saveDontMatch() {
    this.seriesService.updateDontMatch(this.series().id, true).subscribe(() => {
      this.modalService.close(true);
    });
  }



  protected readonly MetadataProvider = MetadataProvider;
  protected readonly ScrobbleProvider = ScrobbleProvider;
  protected readonly PlusMediaFormat = PlusMediaFormat;
  protected readonly allMetadataProviders = AllMetadataProviders;
}
