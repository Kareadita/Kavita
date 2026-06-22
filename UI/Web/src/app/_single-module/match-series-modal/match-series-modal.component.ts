import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  OnInit,
  signal,
  Signal
} from '@angular/core';
import {Series} from "../../_models/series";
import {SeriesService} from "../../_services/series.service";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {NgbActiveModal, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ExternalSeriesMatch} from "../../_models/series-detail/external-series-match";
import {ToastrService} from "ngx-toastr";
import {catchError, filter, of, skip, startWith, tap} from "rxjs";
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
import {MetadataProvider} from "../../_models/kavitaplus/metadata-provider.enum";
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {MetadataProviderTitlePipe} from "../../_pipes/metadata-provider-title.pipe";
import {LibraryTypePipe} from "../../_pipes/library-type.pipe";
import {MangaFormatPipe} from "../../_pipes/manga-format.pipe";
import {PlusMediaFormat} from "../../_models/series-detail/external-series-detail";
import {TranslocoInjectComponent} from "../../shared/_components/transloco-inject/transloco-inject.component";
import {TranslocoSlotDirective} from "../../_directives/transloco-slot.directive";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";

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
    LibraryTypePipe,
    MangaFormatPipe,
    TranslocoInjectComponent,
    TranslocoSlotDirective,
    TagBadgeComponent,


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
    dontMatch: new FormControl(false, []),
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
  lastQuery = signal<string>('');

  protected bodyState = computed<'empty' | 'dont-match' | 'loading' | 'results' | 'no-results'>(() => {
    if (this.isDontMatch()) return 'dont-match';
    if (this.isLoading()) return 'loading';
    if (!this.hasSearched()) return 'empty';
    return this.matches().length > 0 ? 'results' : 'no-results';
  });

  coverImageUrl!: Signal<string>;
  kavitaVolumeCount!: Signal<number>;
  kavitaChapterCount!: Signal<number>;
  seriesDetail = signal<SeriesDetail | null>(null);
  matchInfo = signal<MatchSeriesInfo | null>(null);

  constructor() {
    this.canSaveDontMatch = computed(() => this.isDontMatch() === true && !this.series().dontMatch);
    this.coverImageUrl = computed(() => this.imageService.getSeriesCoverImage(this.series().id));
    this.kavitaVolumeCount = computed(() => (this.seriesDetail()?.volumes ?? []).length);
    this.kavitaChapterCount = computed(() => (this.seriesDetail()?.chapters ?? []).length);

    effect(() => {
      this.seriesService.getMatchInfo(this.series().id).subscribe(res => {
        this.matchInfo.set(res);
      });
    });

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
  }

  ngOnInit() {
    this.formGroup.patchValue({ dontMatch: this.series().dontMatch || false });
    this.seriesService.getSeriesDetail(this.series().id).subscribe(detail => this.seriesDetail.set(detail));
    this.search();
  }

  search() {
    if (this.isDontMatch()) return;

    this.isLoading.set(true);
    this.lastQuery.set(this.formGroup.value.query ?? '');

    const model: any = { ...this.formGroup.value, seriesId: this.series().id };

    this.seriesService.matchSeries(model).pipe(
      tap(results => {
        this.isLoading.set(false);
        this.hasSearched.set(true);
        this.matches.set(results);
      }),
      catchError(() => {
        this.isLoading.set(false);
        this.hasSearched.set(true);
        return of([]);
      })
    ).subscribe();
  }

  clearQuery() {
    this.formGroup.get('query')?.setValue('');
  }

  matchKey(item: ExternalSeriesMatch) {
    return `${item.series.provider}_${item.series.mangabakaId}_${item.series.hardcoverId}_${item.series.cbrId}_${item.series.aniListId}_${item.series.malId}`;
  }

  selectItem(item: ExternalSeriesMatch) {
    this.selectedItem.set(item);
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

    this.seriesService.updateMatch(this.series().id, data).subscribe(() => {
      this.toastr.success(translate('toasts.match-success'));
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
}
