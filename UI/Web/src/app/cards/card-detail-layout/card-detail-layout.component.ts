import {DOCUMENT, NgClass, NgForOf, NgTemplateOutlet} from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild,
  DestroyRef,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  Inject,
  Input,
  OnChanges,
  OnInit,
  Output,
  SimpleChange,
  SimpleChanges,
  TemplateRef,
  TrackByFunction,
  ViewChild
} from '@angular/core';
import {NavigationStart, Router} from '@angular/router';
import {VirtualScrollerComponent, VirtualScrollerModule} from '@iharbeck/ngx-virtual-scroller';
import {FilterSettings} from 'src/app/metadata-filter/filter-settings';
import {FilterUtilitiesService} from 'src/app/shared/_services/filter-utilities.service';
import {Breakpoint, UtilityService} from 'src/app/shared/_services/utility.service';
import {JumpKey} from 'src/app/_models/jumpbar/jump-key';
import {Library} from 'src/app/_models/library/library';
import {Pagination} from 'src/app/_models/pagination';
import {FilterEvent, FilterItem, SortField} from 'src/app/_models/metadata/series-filter';
import {ActionItem} from 'src/app/_services/action-factory.service';
import {JumpbarService} from 'src/app/_services/jumpbar.service';
import {LoadingComponent} from "../../shared/loading/loading.component";
import {MetadataFilterComponent} from "../../metadata-filter/metadata-filter.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {filter, map} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {tap} from "rxjs";
import {FilterV2} from "../../_models/metadata/v2/filter-v2";


const ANIMATION_TIME_MS = 0;

/**
 * Provides a virtualized card layout, jump bar, and metadata filter bar.
 *
 * How to use:
 * - For filtering:
 *    - pass a filterSettings which will bootstrap the filtering bar
 *    - pass a jumpbar method binding to calc the count for the entity
 * - For card layout
 *    - Pass an identity function for trackby
 *    - Pass a pagination object for the total count
 *    - Pass the items
 *    -
 */
@Component({
  selector: 'app-card-detail-layout',
  imports: [LoadingComponent, VirtualScrollerModule, CardActionablesComponent, MetadataFilterComponent,
    TranslocoDirective, NgTemplateOutlet, NgClass, NgForOf],
  templateUrl: './card-detail-layout.component.html',
  styleUrls: ['./card-detail-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardDetailLayoutComponent implements OnInit, OnChanges {

  private readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly utilityService = inject(UtilityService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly Breakpoint = Breakpoint;

  @Input() header: string = '';
  @Input() isLoading: boolean = false;
  @Input() items: any[] = [];
  @Input() pagination!: Pagination;
  /**
   * Parent scroll for virtualize pagination
   */
  @Input() parentScroll!: Element | Window;

  // Filter Code
  @Input() filterOpen!: EventEmitter<boolean>;
  /**
   * Should filtering be shown on the page
   */
  @Input() filteringDisabled: boolean = false;
  /**
   * Any actions to exist on the header for the parent collection (library, collection)
   */
  @Input() actions: ActionItem<any>[] = [];
  /**
   * A trackBy to help with rendering. This is required as without it there are issues when scrolling
   */
  @Input({required: true}) trackByIdentity!: TrackByFunction<any>;
  @Input() filterSettings!: FilterSettings<number>;
  @Input() refresh!: EventEmitter<void>;
  /**
   * Pass the filter object optionally. If not passed, will create a SeriesFilter by default
   */
  filter: FilterV2<number> | undefined = undefined;

  /**
   * Will force the jumpbar to be disabled - in cases where you're not using a traditional filter config
   */
  @Input() customSort: boolean = false;
  @Input() jumpBarKeys: Array<JumpKey> = []; // This is approx 784 pixels tall, original keys
  jumpBarKeysToRender: Array<JumpKey> = []; // What is rendered on screen

  @Output() itemClicked: EventEmitter<any> = new EventEmitter();
  @Output() applyFilter: EventEmitter<FilterEvent> = new EventEmitter();

  @ContentChild('cardItem') itemTemplate!: TemplateRef<any>;
  @ContentChild('noData') noDataTemplate: TemplateRef<any> | null = null;
  @ViewChild('.jump-bar') jumpBar!: ElementRef<HTMLDivElement>;

  @ViewChild(VirtualScrollerComponent) private virtualScroller!: VirtualScrollerComponent;

  libraries: Array<FilterItem<Library>> = [];

  updateApplied: number = 0;
  bufferAmount: number = 1;




  constructor(@Inject(DOCUMENT) private document: Document) {}


  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  resizeJumpBar() {
    const currentSize = (this.document.querySelector('.viewport-container')?.getBoundingClientRect().height || 10) - 30;
    this.jumpBarKeysToRender = this.jumpbarService.generateJumpBar(this.jumpBarKeys, currentSize);
    this.cdRef.markForCheck();
  }

  ngOnInit(): void {
    if (this.trackByIdentity === undefined) {
      this.trackByIdentity = (_: number, item: any) => `${this.header}_${this.updateApplied}_${item?.libraryId}`;
    }
    // This shouldn't be needed as filter is a temp variable to check if a custom sort is present
    // if (!this.filter) {
    //   this.filter = this.filterUtilityService.createSeriesV2Filter();
    // }

    if (this.filterSettings === undefined) {
      this.filterSettings = new FilterSettings();
      this.cdRef.markForCheck();
    }

    if (this.pagination === undefined) {
      this.pagination = {currentPage: 1, itemsPerPage: this.items.length, totalItems: this.items.length, totalPages: 1};
      this.cdRef.markForCheck();
    }

    if (this.refresh) {
      this.refresh.subscribe(() => {
        this.cdRef.markForCheck();
        this.virtualScroller.refresh();
      });
    }

    this.router.events.pipe(
      filter(event => event instanceof NavigationStart),
      takeUntilDestroyed(this.destroyRef),
      map(evt => evt as NavigationStart),
      tap(_ => this.tryToSaveJumpKey()),
    ).subscribe();

  }


  ngOnChanges(changes: SimpleChanges): void {
    this.jumpBarKeysToRender = [...this.jumpBarKeys];
    this.resizeJumpBar();

    const startIndex = this.jumpbarService.getResumePosition(this.router.url);
    if (startIndex > 0) {
      setTimeout(() => this.virtualScroller.scrollToIndex(startIndex, true, 0, ANIMATION_TIME_MS), 10);
      return;
    }

    if (changes.hasOwnProperty('isLoading')) {
      const loadingChange = changes['isLoading'] as SimpleChange;
      if (loadingChange.previousValue === true && loadingChange.currentValue === false) {
        setTimeout(() => this.virtualScroller.scrollToIndex(0, true, 0, ANIMATION_TIME_MS), 10);
      }
    }
  }

  hasCustomSort() {
    if (this.customSort) return true;
    if (this.filteringDisabled) return false;

    return this.filter?.sortOptions?.sortField != SortField.SortName || !this.filter?.sortOptions.isAscending;
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, undefined);
    }
  }

  applyMetadataFilter(event: FilterEvent) {
    this.applyFilter.emit(event);
    this.updateApplied++;
    this.filter = event.filterV2;
    this.cdRef.markForCheck();
  }


  scrollTo(jumpKey: JumpKey) {
    if (this.hasCustomSort()) return;

    let targetIndex = 0;
    for(let i = 0; i < this.jumpBarKeys.length; i++) {
      if (this.jumpBarKeys[i].key === jumpKey.key) break;
      targetIndex += this.jumpBarKeys[i].size;
    }

    this.virtualScroller.scrollToIndex(targetIndex, true, 0, ANIMATION_TIME_MS);
    setTimeout(() => this.jumpbarService.saveResumePosition(this.router.url, this.virtualScroller.viewPortInfo.startIndex), ANIMATION_TIME_MS + 100);
  }

  tryToSaveJumpKey() {
    this.jumpbarService.saveResumePosition(this.router.url, this.virtualScroller.viewPortInfo.startIndex);
  }
}
