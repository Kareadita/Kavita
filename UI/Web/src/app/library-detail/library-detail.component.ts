import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  HostListener,
  inject,
  OnDestroy,
  OnInit
} from '@angular/core';
import {Title} from '@angular/platform-browser';
import {ActivatedRoute, Router} from '@angular/router';
import {catchError, of, Subject, throwError} from 'rxjs';
import {take, takeUntil} from 'rxjs/operators';
import {BulkSelectionService} from '../cards/bulk-selection.service';
import {KEY_CODES, UtilityService} from '../shared/_services/utility.service';
import {SeriesAddedEvent} from '../_models/events/series-added-event';
import {Library} from '../_models/library';
import {Pagination} from '../_models/pagination';
import {Series} from '../_models/series';
import {FilterEvent, SeriesFilter, SortField} from '../_models/metadata/series-filter';
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
import {MetadataService} from '../_services/metadata.service';
import {FilterComparison} from '../_models/metadata/v2/filter-comparison';
import {FilterField} from '../_models/metadata/v2/filter-field';
import {SeriesFilterV2} from '../_models/metadata/v2/series-filter-v2';

@Component({
  selector: 'app-library-detail',
  templateUrl: './library-detail.component.html',
  styleUrls: ['./library-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LibraryDetailComponent implements OnInit, OnDestroy {

  libraryId!: number;
  libraryName = '';
  series: Series[] = [];
  loadingSeries = false;
  pagination!: Pagination;
  actions: ActionItem<Library>[] = [];
  filter: SeriesFilter | undefined = undefined;
  filterV2: SeriesFilterV2 | undefined = undefined;
  onDestroy: Subject<void> = new Subject<void>();
  filterSettings: FilterSettings = new FilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActive: boolean = false;
  filterActiveCheck!: SeriesFilterV2;
  refresh: EventEmitter<void> = new EventEmitter();

  jumpKeys: Array<JumpKey> = [];

  tabs: Array<{title: string, fragment: string, icon: string}> = [
    {title: 'Library', fragment: '', icon: 'fa-landmark'},
    {title: 'Recommended', fragment: 'recomended', icon: 'fa-award'},
  ];
  active = this.tabs[0];

  metadataService = inject(MetadataService);


  bulkActionCallback = (action: ActionItem<any>, data: any) => {
    const selectedSeriesIndexes = this.bulkSelectionService.getSelectedCardsForSource('series');
    const selectedSeries = this.series.filter((series, index: number) => selectedSeriesIndexes.includes(index + ''));

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
        this.actionService.deleteMultipleSeries(selectedSeries, (successful) => {
          if (!successful) return;
          this.bulkSelectionService.deselectAll();
          this.loadPage();
        });
        break;
    }
  }

  constructor(private route: ActivatedRoute, private router: Router, private seriesService: SeriesService,
    private libraryService: LibraryService, private titleService: Title, private actionFactoryService: ActionFactoryService,
    private actionService: ActionService, public bulkSelectionService: BulkSelectionService, private hubService: MessageHubService,
    private utilityService: UtilityService, public navService: NavService, private filterUtilityService: FilterUtilitiesService,
    private readonly cdRef: ChangeDetectorRef) {
    const routeId = this.route.snapshot.paramMap.get('libraryId');
    if (routeId === null) {
      this.router.navigateByUrl('/libraries');
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



    this.pagination = this.filterUtilityService.pagination(this.route.snapshot);
    [this.filterSettings.presets, this.filterSettings.openByDefault] = this.filterUtilityService.filterPresetsFromUrl(this.route.snapshot);
    if (this.filterSettings.presets) this.filterSettings.presets.libraries = [this.libraryId];

    const filterName = (this.route.snapshot.queryParamMap.get('filterName') || '').trim();
    this.metadataService.getFilter(filterName)
      .subscribe((filter: SeriesFilterV2 | null) => {
        console.log('resume from filter setup')
        if (filter) {
          this.filterV2 = filter;
        } else {
          this.filterV2 = {
            groups: [this.createRootGroup()],
            limitTo: 0,
            sortOptions: {
              isAscending: true,
              sortField: SortField.SortName
            }
          };
          // Update url without an id
          //
          // this.filterV2.groups[0].id = 'lib-1';
          // this.filterV2.groups[0].statements.push(this.metadataService.createDefaultFilterStatement(FilterField.Libraries, FilterComparison.Equal, this.libraryId + ''));
        }

        this.filterSettings.presetsV2 = this.filterV2;
        console.log(this.filterV2);
        this.loadPage();
        this.cdRef.markForCheck();
      });

    this.filterSettings.libraryDisabled = true;
    this.cdRef.markForCheck();
  }

  createRootGroup() {
    const group = this.metadataService.createDefaultFilterGroup();
    const stmt = this.metadataService.createDefaultFilterStatement();
    stmt.comparison = FilterComparison.Contains;
    stmt.field = FilterField.Libraries;
    stmt.value = this.libraryId + '';
    group.id = 'or-1';
    group.statements.push(stmt);

    const rootGroup = this.metadataService.createDefaultFilterGroup();
    rootGroup.id = 'root';
    rootGroup.or.push(group);
    return rootGroup;
  }

  ngOnInit(): void {
    this.hubService.messages$.pipe(takeUntil(this.onDestroy)).subscribe((event) => {
      if (event.event === EVENTS.SeriesAdded) {
        const seriesAdded = event.payload as SeriesAddedEvent;
        if (seriesAdded.libraryId !== this.libraryId) return;
        if (!this.utilityService.deepEqual(this.filter, this.filterActiveCheck)) {
          this.loadPage();
          return;
        }
        this.seriesService.getSeries(seriesAdded.seriesId).subscribe(s => {
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
          this.loadPage();
          return;
        }

        this.series = this.series.filter(s => s.id != seriesRemoved.seriesId);
        this.pagination.totalItems--;
        this.cdRef.markForCheck();
        this.refresh.emit();
      }
    });
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
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

  handleAction(action: ActionItem<Library>, library: Library) {
    let lib: Partial<Library> = library;
    if (library === undefined) {
      lib = {id: this.libraryId, name: this.libraryName};
    }
    switch (action.action) {
      case(Action.Scan):
        this.actionService.scanLibrary(lib);
        break;
      case(Action.RefreshMetadata):
        this.actionService.refreshMetadata(lib);
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
    console.log('library detail, updateFilter occurred: ', data);
    if (data.filterV2 === undefined) return;
    this.filter = data.filter;
    this.filterV2 = data.filterV2;

    if (!data.isFirst) this.filterUtilityService.updateUrlFromFilter(this.pagination, this.filter);
    this.loadPage();
  }

  loadPage() {
    this.loadingSeries = true;
    this.filterActive = !this.utilityService.deepEqual(this.filterV2, this.filterActiveCheck);
    this.cdRef.markForCheck();

    this.seriesService.getSeriesForLibraryV2(undefined, undefined, this.filterV2).pipe(take(1)).subscribe(series => {
      this.series = series.result;
      this.pagination = series.pagination;
      this.loadingSeries = false;
      this.cdRef.markForCheck();
      window.scrollTo(0, 0);
    });
  }

  seriesClicked(series: Series) {
    this.router.navigate(['library', this.libraryId, 'series', series.id]);
  }

  trackByIdentity = (index: number, item: Series) => `${item.id}_${item.name}_${item.localizedName}_${item.pagesRead}`;
}
