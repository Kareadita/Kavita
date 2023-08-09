import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  HostListener,
  inject,
  OnInit
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import {of, Subject} from 'rxjs';
import { debounceTime, take, takeUntil } from 'rxjs/operators';
import { BulkSelectionService } from '../cards/bulk-selection.service';
import { KEY_CODES, UtilityService } from '../shared/_services/utility.service';
import { SeriesAddedEvent } from '../_models/events/series-added-event';
import { Library } from '../_models/library';
import { Pagination } from '../_models/pagination';
import { Series } from '../_models/series';
import {FilterEvent, SeriesFilter, SortField} from '../_models/metadata/series-filter';
import { Action, ActionFactoryService, ActionItem } from '../_services/action-factory.service';
import { ActionService } from '../_services/action.service';
import { LibraryService } from '../_services/library.service';
import { EVENTS, MessageHubService } from '../_services/message-hub.service';
import { SeriesService } from '../_services/series.service';
import { NavService } from '../_services/nav.service';
import { FilterUtilitiesService } from '../shared/_services/filter-utilities.service';
import { FilterSettings } from '../metadata-filter/filter-settings';
import { JumpKey } from '../_models/jumpbar/jump-key';
import { SeriesRemovedEvent } from '../_models/events/series-removed-event';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SentenceCasePipe } from '../pipe/sentence-case.pipe';
import { BulkOperationsComponent } from '../cards/bulk-operations/bulk-operations.component';
import { SeriesCardComponent } from '../cards/series-card/series-card.component';
import { CardDetailLayoutComponent } from '../cards/card-detail-layout/card-detail-layout.component';
import { LibraryRecommendedComponent } from './library-recommended/library-recommended.component';
import { NgFor, NgIf, DecimalPipe } from '@angular/common';
import { NgbNav, NgbNavItem, NgbNavItemRole, NgbNavLink, NgbNavContent, NgbNavOutlet } from '@ng-bootstrap/ng-bootstrap';
import { SideNavCompanionBarComponent } from '../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component';
import {TranslocoDirective, TranslocoService} from "@ngneat/transloco";
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import {MetadataService} from "../_services/metadata.service";
import {FilterComparison} from "../_models/metadata/v2/filter-comparison";
import {FilterField} from "../_models/metadata/v2/filter-field";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";

@Component({
    selector: 'app-library-detail',
    templateUrl: './library-detail.component.html',
    styleUrls: ['./library-detail.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [SideNavCompanionBarComponent, CardActionablesComponent, NgbNav, NgFor, NgbNavItem, NgbNavItemRole, NgbNavLink, NgbNavContent, NgIf, LibraryRecommendedComponent, CardDetailLayoutComponent, SeriesCardComponent, BulkOperationsComponent, NgbNavOutlet, DecimalPipe, SentenceCasePipe, TranslocoDirective]
})
export class LibraryDetailComponent implements OnInit {

  libraryId!: number;
  libraryName = '';
  series: Series[] = [];
  loadingSeries = false;
  pagination!: Pagination;
  actions: ActionItem<Library>[] = [];
  filter: SeriesFilter | undefined = undefined;
  filterV2: SeriesFilterV2 | undefined = undefined;
  filterSettings: FilterSettings = new FilterSettings();
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filterActive: boolean = false;
  filterActiveCheck!: SeriesFilterV2;
  refresh: EventEmitter<void> = new EventEmitter();

  jumpKeys: Array<JumpKey> = [];

  translocoService = inject(TranslocoService);

  tabs: Array<{title: string, fragment: string, icon: string}> = [
    {title: 'library-tab', fragment: '', icon: 'fa-landmark'},
    {title: 'recommended-tab', fragment: 'recommended', icon: 'fa-award'},
  ];
  active = this.tabs[0];
  private readonly destroyRef = inject(DestroyRef);
  private readonly metadataService = inject(MetadataService);
  private readonly cdRef = inject(ChangeDetectorRef);


  bulkActionCallback = (action: ActionItem<any>, data: any) => {
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
    private utilityService: UtilityService, public navService: NavService, private filterUtilityService: FilterUtilitiesService) {
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

    this.actions = this.actionFactoryService.getLibraryActions(this.handleAction.bind(this));

    this.pagination = this.filterUtilityService.pagination(this.route.snapshot);
    [this.filterSettings.presets, this.filterSettings.openByDefault] = this.filterUtilityService.filterPresetsFromUrl(this.route.snapshot);
    this.filterSettings.presetsV2 = this.filterUtilityService.filterPresetsFromUrlV2(this.route.snapshot);
    if (this.filterSettings.presets) this.filterSettings.presets.libraries = [this.libraryId];

    const filterName = (this.route.snapshot.queryParamMap.get('filterName') || '').trim();

    if (filterName === '') {
      this.filterV2 = this.createRootGroup();
      this.filterSettings.presetsV2 = this.filterV2;
      this.loadPage();
    } else {
      this.metadataService.getFilter(filterName)
        .subscribe((filter: SeriesFilterV2 | null) => {
          console.log('resume from filter setup')
          if (filter) {
            this.filterV2 = filter;
          } else {
            this.filterV2 = this.createRootGroup();
            // Update url without an id

          }

          this.filterSettings.presetsV2 = this.filterV2;
          console.log(this.filterV2);
          this.loadPage();
        });
    }


    this.filterSettings.libraryDisabled = true;
    this.cdRef.markForCheck();
  }

  createRootGroup() {
    const stmt = this.metadataService.createDefaultFilterStatement();
    stmt.comparison = FilterComparison.Contains;
    stmt.field = FilterField.Libraries;
    stmt.value = this.libraryId + '';
    const rootGroup = this.metadataService.createDefaultFilterDto();
    rootGroup.statements.push(stmt);

    return rootGroup;
  }

  ngOnInit(): void {
    this.hubService.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      if (event.event === EVENTS.SeriesAdded) {
        const seriesAdded = event.payload as SeriesAddedEvent;
        if (seriesAdded.libraryId !== this.libraryId) return;
        if (!this.utilityService.deepEqual(this.filter, this.filterActiveCheck)) {
          this.loadPage();
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

    if (!data.isFirst) {
      const url = this.filterUtilityService.encodeSeriesFilter(this.filterV2);
      window.history.replaceState(window.location.href, '', url);
    }


    this.loadPage();
  }

  loadPage() {
    this.loadingSeries = true;
    this.filterActive = !this.utilityService.deepEqual(this.filterV2, this.filterActiveCheck);
    this.cdRef.markForCheck();

    this.seriesService.getSeriesForLibraryV2(undefined, undefined, this.filterV2)
      .subscribe(series => {
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
