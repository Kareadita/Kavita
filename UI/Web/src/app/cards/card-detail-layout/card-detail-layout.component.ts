import {CommonModule, DOCUMENT} from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  Inject,
  Input,
  OnChanges,
  OnInit,
  Output,
  TemplateRef,
  TrackByFunction,
  ViewChild
} from '@angular/core';
import {Router} from '@angular/router';
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
import {ScrollService} from 'src/app/_services/scroll.service';
import {LoadingComponent} from "../../shared/loading/loading.component";


import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {MetadataFilterComponent} from "../../metadata-filter/metadata-filter.component";
import {TranslocoDirective} from "@ngneat/transloco";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {SeriesFilterV2} from "../../_models/metadata/v2/series-filter-v2";


const ANIMATION_TIME_MS = 0;

@Component({
  selector: 'app-card-detail-layout',
  standalone: true,
  imports: [CommonModule, LoadingComponent, VirtualScrollerModule, CardActionablesComponent, NgbTooltip, MetadataFilterComponent, TranslocoDirective],
  templateUrl: './card-detail-layout.component.html',
  styleUrls: ['./card-detail-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CardDetailLayoutComponent implements OnInit, OnChanges {

  private readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly utilityService = inject(UtilityService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly router = inject(Router);
  private readonly scrollService = inject(ScrollService);

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
  @Input() filterSettings!: FilterSettings;
  @Input() refresh!: EventEmitter<void>;


  @Input() jumpBarKeys: Array<JumpKey> = []; // This is approx 784 pixels tall, original keys
  jumpBarKeysToRender: Array<JumpKey> = []; // What is rendered on screen

  @Output() itemClicked: EventEmitter<any> = new EventEmitter();
  @Output() applyFilter: EventEmitter<FilterEvent> = new EventEmitter();

  @ContentChild('cardItem') itemTemplate!: TemplateRef<any>;
  @ContentChild('noData') noDataTemplate!: TemplateRef<any>;
  @ViewChild('.jump-bar') jumpBar!: ElementRef<HTMLDivElement>;

  @ViewChild(VirtualScrollerComponent) private virtualScroller!: VirtualScrollerComponent;

  filter: SeriesFilterV2 = this.filterUtilityService.createSeriesV2Filter();
  libraries: Array<FilterItem<Library>> = [];

  updateApplied: number = 0;
  hasResumedJumpKey: boolean = false;
  bufferAmount: number = 1;

  protected readonly Breakpoint = Breakpoint;

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
  }


  ngOnChanges(): void {
    this.jumpBarKeysToRender = [...this.jumpBarKeys];
    this.resizeJumpBar();

    // TODO: I wish I had signals so I can tap into when isLoading is false and trigger the scroll code

    // Don't resume jump key when there is a custom sort order, as it won't work
    if (!this.hasCustomSort()) {
      if (!this.hasResumedJumpKey && this.jumpBarKeysToRender.length > 0) {
        const resumeKey = this.jumpbarService.getResumeKey(this.router.url);
        if (resumeKey === '') return;
        const keys = this.jumpBarKeysToRender.filter(k => k.key === resumeKey);
        if (keys.length < 1) return;

        this.hasResumedJumpKey = true;
        setTimeout(() => this.scrollTo(keys[0]), 100);
      }
    }
    //  else {
    //   // I will come back and refactor this to work
    //   // const scrollPosition = this.jumpbarService.getResumePosition(this.router.url);
    //   // console.log('scroll position: ', scrollPosition);
    //   // if (scrollPosition > 0) {
    //   //   setTimeout(() => this.virtualScroller.scrollToIndex(scrollPosition, true, 0, 1000), 100);
    //   // }
    // }
  }

  hasCustomSort() {
    if (this.filteringDisabled) return false;
    return this.filter?.sortOptions?.sortField != SortField.SortName || !this.filter?.sortOptions.isAscending
      || this.filterSettings?.presetsV2?.sortOptions?.sortField != SortField.SortName || !this.filterSettings?.presetsV2?.sortOptions?.isAscending;
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
    this.jumpbarService.saveResumeKey(this.router.url, jumpKey.key);
    // TODO: This doesn't work, we need the offset from virtual scroller
    this.jumpbarService.saveScrollOffset(this.router.url, this.scrollService.scrollPosition);

    this.cdRef.markForCheck();
  }

  tryToSaveJumpKey(item: any) {
    let name = '';
    if (item.hasOwnProperty('name')) {
      name = item.name;
    } else if (item.hasOwnProperty('title')) {
      name = item.title;
    }
    this.jumpbarService.saveResumeKey(this.router.url, name.charAt(0));
  }
}
