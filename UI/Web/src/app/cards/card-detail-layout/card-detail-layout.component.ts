import {DOCUMENT, NgClass, NgTemplateOutlet} from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  ContentChild,
  DestroyRef,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  input,
  Input,
  OnChanges,
  OnInit,
  Output,
  signal,
  Signal,
  SimpleChanges,
  TemplateRef,
  TrackByFunction,
  ViewChild,
  WritableSignal
} from '@angular/core';
import {NavigationStart, Router} from '@angular/router';
import {VirtualScrollerComponent, VirtualScrollerModule} from '@iharbeck/ngx-virtual-scroller';
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
import {FilterSettingsBase, ValidFilterEntity} from "../../metadata-filter/filter-settings";


const ANIMATION_TIME_MS = 0;

/**
 * Provides a virtualized card layout, jump bar, and metadata filter bar.
 *
 * How to use:
 * - For filtering:
 *    - pass a filterSettings which will bootstrap the filtering bar
 *    - pass a jumpbar method binding to calc the count for the entity (not implemented yet)
 * - For card layout
 *    - Pass an identity function for trackby
 *    - Pass a pagination object for the total count
 *    - Pass the items
 *    -
 */
@Component({
  selector: 'app-card-detail-layout',
  imports: [LoadingComponent, VirtualScrollerModule, CardActionablesComponent, MetadataFilterComponent, TranslocoDirective, NgTemplateOutlet, NgClass],
  templateUrl: './card-detail-layout.component.html',
  styleUrls: ['./card-detail-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true
})
export class CardDetailLayoutComponent<TFilter extends number, TSort extends number> implements OnInit, OnChanges, AfterViewInit {
  private document = inject<Document>(DOCUMENT);


  protected readonly utilityService = inject(UtilityService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);



  header: Signal<string> = input('');
  @Input() isLoading: boolean = false;
  @Input() pagination!: Pagination;
  @Input() items: any[] = [];


  /**
   * Parent scroll for virtualize pagination
   */
  @Input() parentScroll!: Element | Window;

  // We need to pass filterOpen from the grandfather to the metadata filter due to the filter button being in a separate component
  @Input() filterOpen!: EventEmitter<boolean>;
  /**
   * Should filtering be shown on the page
   */
  @Input() filteringDisabled: boolean = false;
  /**
   * Any actions to exist on the header for the parent collection (library, collection)
   */
  actions: Signal<ActionItem<any>[]> = input([]);
  /**
   * A trackBy to help with rendering. This is required as without it there are issues when scrolling
   */
  @Input({required: true}) trackByIdentity!: TrackByFunction<any>;
  @Input() filterSettings: FilterSettingsBase | undefined = undefined;
  entityType = input<ValidFilterEntity | 'other'>();
  @Input() refresh!: EventEmitter<void>;


  /**
   * Will force the jumpbar to be disabled - in cases where you're not using a traditional filter config
   */
  customSort = input(false);
  @Input() jumpBarKeys: Array<JumpKey> = []; // This is approx 784 pixels tall, original keys
  jumpBarKeysToRender: Array<JumpKey> = []; // What is rendered on screen

  @Output() itemClicked: EventEmitter<any> = new EventEmitter();
  @Output() applyFilter: EventEmitter<FilterEvent> = new EventEmitter();

  @ContentChild('cardItem') itemTemplate!: TemplateRef<any>;
  @ContentChild('noData') noDataTemplate: TemplateRef<any> | null = null;
  @ViewChild('.jump-bar') jumpBar!: ElementRef<HTMLDivElement>;
  /**
   * Template that is rendered next to the save button
   */
  @ContentChild('extraButtons') extraButtonsRef!: TemplateRef<any>;
  /**
   * Template that is rendered above the grid, but always below the filter
   */
  @ContentChild('topBar') topBar!: TemplateRef<any>;

  @ViewChild(VirtualScrollerComponent) private virtualScroller!: VirtualScrollerComponent;

  libraries: Array<FilterItem<Library>> = [];

  updateApplied: number = 0;
  bufferAmount: number = 1;
  resumed: boolean = false;


  filterSignal: WritableSignal<FilterV2<number, number> | undefined> = signal(undefined);
  hasCustomSort = computed(() => {
    if (this.customSort()) return true;
    if (this.filteringDisabled) return false;

    const filter = this.filterSignal();
    return filter?.sortOptions?.sortField != SortField.SortName || !filter?.sortOptions.isAscending;
  });


  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  resizeJumpBar() {
    const currentSize = (this.document.querySelector('.viewport-container')?.getBoundingClientRect().height || 10) - 30;
    this.jumpBarKeysToRender = this.jumpbarService.generateJumpBar(this.jumpBarKeys, currentSize);
    this.cdRef.markForCheck();
  }

  ngOnInit(): void {
    if (this.trackByIdentity === undefined) {
      this.trackByIdentity = (_: number, item: any) => `${this.header()}_${this.updateApplied}_${item?.id}`;
    }

    if (this.pagination === undefined) {
      const items = this.items;
      this.pagination = {currentPage: 1, itemsPerPage: items.length, totalItems: items.length, totalPages: 1};
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
      tap(_ => this.tryToSaveJumpKey({})),
    ).subscribe();

  }

  ngAfterViewInit(): void {
    if (this.resumed) return;

    this.jumpBarKeysToRender = [...this.jumpBarKeys];
    this.resizeJumpBar();

    if (this.jumpBarKeysToRender.length > 0) {
      this.resumed = true;

      // Check if there is an exact scroll position to restore
      const scrollOffset = this.jumpbarService.getResumePosition(this.router.url);
      if (scrollOffset > 0) {
        setTimeout(() => {
          this.virtualScroller.scrollToPosition(scrollOffset, ANIMATION_TIME_MS);
        }, 100)
      } else {
        const resumeKey = this.jumpbarService.getResumeKey(this.router.url);
        if (resumeKey === '') return;
        const keys = this.jumpBarKeysToRender.filter(k => k.key === resumeKey);
        if (keys.length < 1) return;

        setTimeout(() => this.scrollTo(keys[0]), 100);
      }
    }
  }


  ngOnChanges(changes: SimpleChanges): void {
    this.jumpBarKeysToRender = [...this.jumpBarKeys];
    this.resizeJumpBar();

    if (this.jumpBarKeysToRender.length > 0) {
      this.resumed = true;

      // Check if there is an exact scroll position to restore
      const scrollOffset = this.jumpbarService.getResumePosition(this.router.url);
      if (scrollOffset > 0) {
        setTimeout(() => {
          this.virtualScroller.scrollToPosition(scrollOffset, ANIMATION_TIME_MS);
        }, 100)
      } else {
        const resumeKey = this.jumpbarService.getResumeKey(this.router.url);
        if (resumeKey === '') return;
        const keys = this.jumpBarKeysToRender.filter(k => k.key === resumeKey);
        if (keys.length < 1) return;

        setTimeout(() => this.scrollTo(keys[0]), 100);
      }
    }
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, undefined);
    }
  }

  applyMetadataFilter(event: FilterEvent<number, number>) {
    this.applyFilter.emit(event as FilterEvent<TFilter, TSort>);
    this.updateApplied++;
    this.filterSignal.set(event.filterV2);
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

  tryToSaveJumpKey(item: any) {
    let name = '';
    if (item.hasOwnProperty('sortName')) {
      name = item.sortName;
    } else if (item.hasOwnProperty('seriesName')) {
      name = item.seriesName;
    } else if (item.hasOwnProperty('name')) {
      name = item.name;
    } else if (item.hasOwnProperty('title')) {
      name = item.title;
    }
    this.jumpbarService.saveResumeKey(this.router.url, name.charAt(0));
    this.jumpbarService.saveResumePosition(this.router.url, this.virtualScroller.viewPortInfo.scrollStartPosition);
  }

  protected readonly Breakpoint = Breakpoint;
}
