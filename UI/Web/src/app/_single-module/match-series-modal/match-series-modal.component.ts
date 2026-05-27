import {ChangeDetectionStrategy, Component, computed, effect, inject, input, OnInit, signal} from '@angular/core';
import {Series} from "../../_models/series";
import {SeriesService} from "../../_services/series.service";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {NgbActiveModal, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ExternalSeriesMatch} from "../../_models/series-detail/external-series-match";
import {ToastrService} from "ngx-toastr";
import {catchError, filter, of, startWith, tap} from "rxjs";
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {EmptyStateComponent} from "../../shared/_components/empty-state/empty-state.component";
import {
  MatchSeriesResultItemComponent
} from "../../shared/_components/match-series-result-item/match-series-result-item.component";
import {ImageComponent} from "../../shared/image/image.component";
import {ImageService} from "../../_services/image.service";

@Component({
  selector: 'app-match-series-modal',
  imports: [
    ReactiveFormsModule,
    TranslocoDirective,
    NgbTooltip,
    EmptyStateComponent,
    MatchSeriesResultItemComponent,
    ImageComponent,
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

  private readonly _queryDisableEffect = effect(() => {
    if (this.isDontMatch()) {
      this.formGroup.controls.query.disable();
    } else {
      this.formGroup.controls.query.enable();
    }
  });

  private readonly _autoSearchOnEnable = this.formGroup.controls.dontMatch.valueChanges.pipe(
    filter(v => v === false),
    takeUntilDestroyed()
  ).subscribe(() => this.search());

  protected readonly canSaveDontMatch = computed(() =>
    this.isDontMatch() === true && !this.series().dontMatch
  );

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

  protected coverImageUrl = computed(() => this.imageService.getSeriesCoverImage(this.series().id));

  ngOnInit() {
    this.formGroup.patchValue({ dontMatch: this.series().dontMatch || false });
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
}
