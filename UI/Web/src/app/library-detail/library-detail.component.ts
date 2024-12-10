import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  HostListener,
  inject,
  OnInit
} from '@angular/core';
import {Title} from '@angular/platform-browser';
import {ActivatedRoute, Router} from '@angular/router';
import {take} from 'rxjs/operators';
import {BulkSelectionService} from '../cards/bulk-selection.service';
import {KEY_CODES, UtilityService} from '../shared/_services/utility.service';
import {SeriesAddedEvent} from '../_models/events/series-added-event';
import {Library} from '../_models/library/library';
import {Pagination} from '../_models/pagination';
import {Series} from '../_models/series';
import {FilterEvent} from '../_models/metadata/series-filter';
import {Action, ActionFactoryService, ActionItem} from '../_services/action-factory.service';
import {ActionService} from '../_services/action.service';
import {LibraryService} from '../_services/library.service';
import {EVENTS, MessageHubService} from '../_services/message-hub.service';
import {SeriesService} from '../_services/series.service';
import {NavService} from '../_services/nav.service';
import {FilterUtilitiesService} from '../shared/_services/filter-utilities.service';
import {FilterSettings} from '../metadata-filter/filter-settings';
import {JumpKey} from '../_models/jumpbar/jump-key';
import {SeriesRemovedEvent} from '../_models/events/series-removed-event';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {BulkOperationsComponent} from '../cards/bulk-operations/bulk-operations.component';
import {SeriesCardComponent} from '../cards/series-card/series-card.component';
import {CardDetailLayoutComponent} from '../cards/card-detail-layout/card-detail-layout.component';
import {DecimalPipe} from '@angular/common';
import {
  SideNavCompanionBarComponent
} from '../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import {FilterComparison} from "../_models/metadata/v2/filter-comparison";
import {FilterField} from "../_models/metadata/v2/filter-field";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";
import {LoadingComponent} from "../shared/loading/loading.component";
import {debounceTime, ReplaySubject, tap} from "rxjs";

@Component({
  selector: 'app-library-detail',
  templateUrl: './library-detail.component.html',
  styleUrls: ['./library-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [SideNavCompanionBarComponent, CardActionablesComponent,
    CardDetailLayoutComponent, SeriesCardComponent, BulkOperationsComponent, DecimalPipe, TranslocoDirective, LoadingComponent]
})
export class LibraryDetailComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seriesService = inject(SeriesService);
  private readonly libraryService = inject(LibraryService);
  private readonly titleService = inject(Title);
  private readonly actionFactoryService = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly hubService = inject(MessageHubService);
  private readonly utilityService = inject(UtilityService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  public readonly navService = inject(NavService);
  public readonly bulkSelectionService = inject(BulkSelectionService);

  libraryId!: number;
  libraryName = '';
  series: Series[] = [];
  loadingSeries = false;
  pagination: Pagination = {currentPage: 0, totalPages: 0, totalItems: 0, itemsPerPage: 0};
  actions: ActionItem<Library>[] = [];
  filter: SeriesFilterV2 | undefined = undefined;
  filterSettings: FilterSettings = new FilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActive: boolean = false;
  filterActiveCheck!: SeriesFilterV2;
  refresh: EventEmitter<void> = new EventEmitter();
  jumpKeys: Array<JumpKey> = [];
  bulkLoader: boolean = false;

  tabs: Array<{title: string, fragment: string, icon: string}> = [
    {title: 'library-tab', fragment: '', icon: 'fa-landmark'},
    {title: 'recommended-tab', fragment: 'recommended', icon: 'fa-award'},
  ];
  active = this.tabs[0];

  loadPageSource = new ReplaySubject(1);
  loadPage$ = this.loadPageSource.asObservable();

  bulkActionCallback = async (action: ActionItem<any>, data: any) => {
    const selectedSeriesIndices = this.bulkSelectionService.getSelectedCardsForSource('series');
    const selectedSeries = this.series.filter((series, index: number) => selectedSeriesIndices.includes(index + ''));

    switch (action.action) {
      case Action.AddToReadingList:
        this.actionService.addMultipleSeriesToReadingList(selectedSeries, (success) => {
          if (success) this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.AddToWantToReadList:
        this.actionService.addMultipleSeriesToWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.RemoveFromWantToReadList:
        this.actionService.removeMultipleSeriesFromWantToReadList(selectedSeries.map(s => s.id), () => {
          this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.AddToCollection:
        this.actionService.addMultipleSeriesToCollectionTag(selectedSeries, (success) => {
          if (success) this.bulkSelectionService.deselectAll();
          this.cdRef.markForCheck();
        });
        break;
      case Action.MarkAsRead:
        this.actionService.markMultipleSeriesAsRead(selectedSeries, () => {
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });

        break;
      case Action.MarkAsUnread:
        this.actionService.markMultipleSeriesAsUnread(selectedSeries, () => {
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
      case Action.Delete:
        if (selectedSeries.length > 25) {
          this.bulkLoader = true;
          this.cdRef.markForCheck();
        }

        await this.actionService.deleteMultipleSeries(selectedSeries, (successful) => {
          this.bulkLoader = false;
          this.cdRef.markForCheck();
          if (!successful) return;
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
    }
  }


  constructor() {
    const routeId = this.route.snapshot.paramMap.get('libraryId');
    if (routeId === null) {
      this.router.navigateByUrl('/home');
      return;
    }

    this.actions = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));
    this.router.routeReuseStrategy.shouldReuseRoute = () => false;
    this.libraryId = parseInt(routeId, 10);
    this.libraryService.getLibraryNames().pipe(take(1)).subscribe(names => {
      this.libraryName = names[this.libraryId];
      this.titleService.setTitle('Kavita - ' + this.libraryName);
      this.cdRef.markForCheck();
    });

    this.libraryService.getJumpBar(this.libraryId).subscribe(barDetails => {
      this.jumpKeys = barDetails;
      this.cdRef.markForCheck();
    });

    this.actions = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));

    this.filterUtilityService.filterPresetsFromUrl(this.route.snapshot).subscribe(filter => {
      this.filter = filter;

      if (this.filter.statements.filter(stmt => stmt.field === FilterField.Libraries).length === 0) {
        this.filter!.statements.push({field: FilterField.Libraries, value: this.libraryId + '', comparison: FilterComparison.Equal});
      }

      this.filterActiveCheck = this.filterUtilityService.createSeriesV2Filter();
      this.filterActiveCheck.statements.push({field: FilterField.Libraries, value: this.libraryId + '', comparison: FilterComparison.Equal});

      this.filterSettings.presetsV2 =  this.filter;

      this.loadPage$.pipe(takeUntilDestroyed(this.destroyRef), debounceTime(100), tap(_ => this.loadPage())).subscribe();

      this.cdRef.markForCheck();
    });
  }


  ngOnInit(): void {
    this.hubService.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      if (event.event === EVENTS.SeriesAdded) {
        const seriesAdded = event.payload as SeriesAddedEvent;
        if (seriesAdded.libraryId !== this.libraryId) return;
        if (!this.utilityService.deepEqual(this.filter, this.filterActiveCheck)) {
          this.loadPageSource.next(true);
          return;
        }
        this.seriesService.getSeries(seriesAdded.seriesId).subscribe(s => {
          if (this.series.filter(sObj => s.id === sObj.id).length > 0) return;
          this.series = [...this.series, s].sort((s1: Series, s2: Series) => {
            if (s1.sortName < s2.sortName) return -1;
            if (s1.sortName > s2.sortName) return 1;
            return 0;
          });
          this.pagination.totalItems++;
          this.cdRef.markForCheck();
          this.refresh.emit();
        });


      } else if (event.event === EVENTS.SeriesRemoved) {
        const seriesRemoved = event.payload as SeriesRemovedEvent;
        if (seriesRemoved.libraryId !== this.libraryId) return;
        if (!this.utilityService.deepEqual(this.filter, this.filterActiveCheck)) {
          this.loadPageSource.next(true);
          return;
        }

        this.series = this.series.filter(s => s.id != seriesRemoved.seriesId);
        this.pagination.totalItems--;
        this.cdRef.markForCheck();
        this.refresh.emit();
      }
    });
  }


  @HostListener('document:keydown.shift', ['$event'])
  handleKeypress(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = true;
    }
  }

  @HostListener('document:keyup.shift', ['$event'])
  handleKeyUp(event: KeyboardEvent) {
    if (event.key === KEY_CODES.SHIFT) {
      this.bulkSelectionService.isShiftDown = false;
    }
  }

  async handleAction(action: ActionItem<Library>, library: Library) {
    let lib: Partial<Library> = library;
    if (library === undefined) {
      this.libraryService.getLibrary(this.libraryId).subscribe(async library => {
        switch (action.action) {
          case(Action.Scan):
            await this.actionService.scanLibrary(library);
            break;
          case(Action.RefreshMetadata):
            await this.actionService.refreshLibraryMetadata(library);
            break;
          case(Action.GenerateColorScape):
            await this.actionService.refreshLibraryMetadata(library, undefined, false);
            break;
          case (Action.Delete):
            await this.actionService.deleteLibrary(library, () => {
              this.loadPageSource.next(true);
            });
            break;
          case (Action.AnalyzeFiles):
            await this.actionService.analyzeFiles(library);
            break;
          case(Action.Edit):
            this.actionService.editLibrary(library);
            break;
          default:
            break;
        }
      });
      return
    }
    switch (action.action) {
      case(Action.Scan):
        await this.actionService.scanLibrary(lib);
        break;
      case(Action.RefreshMetadata):
        await this.actionService.refreshLibraryMetadata(lib);
        break;
      case(Action.GenerateColorScape):
        await this.actionService.refreshLibraryMetadata(lib, undefined, false);
        break;
      case(Action.Edit):
        this.actionService.editLibrary(lib);
        break;
      default:
        break;
    }
  }



  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, undefined);
    }
  }

  updateFilter(data: FilterEvent) {
    if (data.filterV2 === undefined) return;
    this.filter = data.filterV2;

    if (data.isFirst) {
      this.loadPageSource.next(true);
      return;
    }

    this.filterUtilityService.updateUrlFromFilter(this.filter).subscribe((encodedFilter) => {
      this.loadPageSource.next(true);
    });
  }

  loadPage() {
    this.loadingSeries = true;
    this.filterActive = !this.utilityService.deepEqual(this.filter, this.filterActiveCheck);
    this.cdRef.markForCheck();

    this.seriesService.getSeriesForLibraryV2(undefined, undefined, this.filter)
      .subscribe(series => {
        this.series = series.result;
        this.pagination = series.pagination;
        this.loadingSeries = false;
        this.cdRef.markForCheck();
      });
  }

  trackByIdentity = (index: number, item: Series) => `${item.id}_${item.name}_${item.localizedName}_${item.pagesRead}`;
}
