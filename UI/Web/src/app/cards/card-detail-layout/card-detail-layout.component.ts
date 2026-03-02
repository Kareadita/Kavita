import {DOCUMENT, NgClass, NgTemplateOutlet} from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  computed,
  contentChild,
  DestroyRef,
  effect,
  EventEmitter,
  inject,
  input,
  output,
  signal,
  TemplateRef,
  TrackByFunction,
  viewChild,
  WritableSignal
} from '@angular/core';
import {NavigationStart, Router} from '@angular/router';
import {VirtualScrollerComponent, VirtualScrollerModule} from '@iharbeck/ngx-virtual-scroller';
import {JumpKey} from 'src/app/_models/jumpbar/jump-key';
import {Pagination} from 'src/app/_models/pagination';
import {FilterEvent, SortField} from 'src/app/_models/metadata/series-filter';
import {JumpbarService} from 'src/app/_services/jumpbar.service';
import {LoadingComponent} from "../../shared/loading/loading.component";
import {MetadataFilterComponent} from "../../metadata-filter/metadata-filter.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {filter} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {tap} from "rxjs";
import {FilterV2} from "../../_models/metadata/v2/filter-v2";
import {FilterSettingsBase, ValidFilterEntity} from "../../metadata-filter/filter-settings";
import {ActionItem} from "../../_models/actionables/action-item";
import {ActionResult} from "../../_models/actionables/action-result";


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
 */
@Component({
  selector: 'app-card-detail-layout',
  imports: [LoadingComponent, VirtualScrollerModule, CardActionablesComponent, MetadataFilterComponent, TranslocoDirective, NgTemplateOutlet, NgClass],
  templateUrl: './card-detail-layout.component.html',
  styleUrls: ['./card-detail-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true
})
export class CardDetailLayoutComponent<TFilter extends number, TSort extends number> implements AfterViewInit {
  private readonly document = inject<Document>(DOCUMENT);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);


  header = input('');
  isLoading = input(false);
  pagination = input<Pagination | undefined>();
  items = input<any[]>([]);

  /**
   * Parent scroll for virtualize pagination
   */
  parentScroll = input<Element | Window>();

  // We need to pass filterOpen from the grandfather to the metadata filter due to the filter button being in a separate component
  filterOpen = input<EventEmitter<boolean>>();
  /**
   * Should filtering be shown on the page
   */
  filteringDisabled = input(false);
  /**
   * Any actions to exist on the header for the parent collection (library, collection)
   */
  actions = input<ActionItem<any>[]>([]);
  /**
   * A trackBy to help with rendering. This is required as without it there are issues when scrolling
   */
  trackByIdentity = input.required<TrackByFunction<any>>();
  filterSettings = input<FilterSettingsBase | undefined>();
  entityType = input<ValidFilterEntity | 'other'>();
  refresh = input<EventEmitter<void>>();


  /**
   * Will force the jumpbar to be disabled - in cases where you're not using a traditional filter config
   */
  customSort = input(false);
  jumpBarKeys = input<Array<JumpKey>>([]); // This is approx 784 pixels tall, original keys

  itemClicked = output<any>();
  applyFilter = output<FilterEvent>();

  itemTemplate = contentChild.required<TemplateRef<any>>('cardItem');
  noDataTemplate = contentChild<TemplateRef<any>>('noData');
  /**
   * Template that is rendered next to the save button
   */
  extraButtonsRef = contentChild<TemplateRef<any>>('extraButtons');
  /**
   * Template that is rendered above the grid, but always below the filter
   */
  topBar = contentChild<TemplateRef<any>>('topBar');

  private virtualScroller = viewChild(VirtualScrollerComponent);

  bufferAmount: number = 1;
  resumed: boolean = false;

  private viewportHeight = signal(0);

  filterSignal: WritableSignal<FilterV2<number, number> | undefined> = signal(undefined);

  effectivePagination = computed(() => {
    const p = this.pagination();
    if (p) return p;
    const items = this.items();
    return {currentPage: 1, itemsPerPage: items.length, totalItems: items.length, totalPages: 1};
  });

  jumpBarKeysToRender = computed(() => {
    const keys = this.jumpBarKeys();
    const height = this.viewportHeight();
    return height > 0
      ? this.jumpbarService.generateJumpBar(keys, height)
      : [...keys];
  });

  hasCustomSort = computed(() => {
    if (this.customSort()) return true;
    if (this.filteringDisabled()) return false;

    const filter = this.filterSignal();
    return filter?.sortOptions?.sortField != SortField.SortName || !filter?.sortOptions.isAscending;
  });

  trackItem = (index: number, item: any) => this.trackByIdentity()(index, item);

  constructor() {
    // Save jump key on navigation away
    this.router.events.pipe(
      filter(event => event instanceof NavigationStart),
      takeUntilDestroyed(),
      tap(() => this.tryToSaveJumpKey({})),
    ).subscribe();

    // Subscribe to refresh emitter (with cleanup on change)
    effect((onCleanup) => {
      const refreshEmitter = this.refresh();
      if (!refreshEmitter) return;
      const sub = refreshEmitter.subscribe(() => this.virtualScroller()?.refresh());
      onCleanup(() => sub.unsubscribe());
    });

    // Resume scroll position when jump bar keys change
    effect(() => {
      const keysToRender = this.jumpBarKeysToRender();
      if (keysToRender.length === 0 || this.resumed) return;

      this.resumed = true;
      this.tryResumeScrollPosition(keysToRender);
    });

  }

  ngAfterViewInit() {
    const container = this.document.querySelector('.viewport-container');
    if (!container) return;

    const observer = new ResizeObserver(entries => {
      const h = (entries[0]?.contentRect.height || 10) - 30;
      this.viewportHeight.set(h);
    });

    observer.observe(container);
    this.destroyRef.onDestroy(() => observer.disconnect());
  }

  performAction(event: ActionItem<void> | ActionResult<void>) {
    // Skip ActionResults — they've already been handled
    if ('effect' in event) return;
  }

  applyMetadataFilter(event: FilterEvent<number, number>) {
    this.applyFilter.emit(event as FilterEvent<TFilter, TSort>);
    this.filterSignal.set(event.filterV2);
  }


  scrollTo(jumpKey: JumpKey) {
    if (this.hasCustomSort()) return;

    const keys = this.jumpBarKeys();
    let targetIndex = 0;
    for(let i = 0; i < keys.length; i++) {
      if (keys[i].key === jumpKey.key) break;
      targetIndex += keys[i].size;
    }

    this.virtualScroller()?.scrollToIndex(targetIndex, true, 0, ANIMATION_TIME_MS);
    setTimeout(() => this.jumpbarService.saveResumePosition(this.router.url, this.virtualScroller()!.viewPortInfo.startIndex), ANIMATION_TIME_MS + 100);
  }

  tryToSaveJumpKey(item: any) {
    let name = '';
    if (item.hasOwnProperty('sortName')) {
      name = item.sortName;
    } else if (item.hasOwnProperty('seriesSortName')) { // Reading List Item
      name = item.seriesSortName;
    } else if (item.hasOwnProperty('seriesName')) {
      name = item.seriesName;
    } else if (item.hasOwnProperty('name')) {
      name = item.name;
    } else if (item.hasOwnProperty('title')) {
      name = item.title;
    }
    this.jumpbarService.saveResumeKey(this.router.url, name.charAt(0));
    const scroller = this.virtualScroller();
    if (scroller) {
      this.jumpbarService.saveResumePosition(this.router.url, scroller.viewPortInfo.scrollStartPosition);
    }
  }

  private tryResumeScrollPosition(keysToRender: Array<JumpKey>) {
    // Check if there is an exact scroll position to restore
    const scrollOffset = this.jumpbarService.getResumePosition(this.router.url);
    if (scrollOffset > 0) {
      setTimeout(() => {
        this.virtualScroller()?.scrollToPosition(scrollOffset, ANIMATION_TIME_MS);
      }, 100);
    } else {
      const resumeKey = this.jumpbarService.getResumeKey(this.router.url);
      if (resumeKey === '') return;
      const keys = keysToRender.filter(k => k.key === resumeKey);
      if (keys.length < 1) return;

      setTimeout(() => this.scrollTo(keys[0]), 100);
    }
  }
}
