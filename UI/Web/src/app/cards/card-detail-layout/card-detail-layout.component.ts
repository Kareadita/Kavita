import { CdkVirtualScrollViewport } from '@angular/cdk/scrolling';
import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ContentChild, ElementRef, EventEmitter, HostListener,
   Inject, Input, OnChanges, OnDestroy, OnInit, Output, TemplateRef, TrackByFunction, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { VirtualScrollerComponent } from '@iharbeck/ngx-virtual-scroller';
import { Subject } from 'rxjs';
import { FilterSettings } from 'src/app/metadata-filter/filter-settings';
import { FilterUtilitiesService } from 'src/app/shared/_services/filter-utilities.service';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { Library } from 'src/app/_models/library';
import { Pagination } from 'src/app/_models/pagination';
import { FilterEvent, FilterItem, SeriesFilter } from 'src/app/_models/metadata/series-filter';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { JumpbarService } from 'src/app/_services/jumpbar.service';

@Component({
  selector: 'app-card-detail-layout',
  templateUrl: './card-detail-layout.component.html',
  styleUrls: ['./card-detail-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardDetailLayoutComponent implements OnInit, OnDestroy, OnChanges {

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
  @Input() trackByIdentity!: TrackByFunction<any>; //(index: number, item: any) => string
  @Input() filterSettings!: FilterSettings;
  @Input() refresh!: EventEmitter<void>;


  @Input() jumpBarKeys: Array<JumpKey> = []; // This is aprox 784 pixels tall, original keys
  jumpBarKeysToRender: Array<JumpKey> = []; // What is rendered on screen

  @Output() itemClicked: EventEmitter<any> = new EventEmitter();
  @Output() applyFilter: EventEmitter<FilterEvent> = new EventEmitter();

  @ContentChild('cardItem') itemTemplate!: TemplateRef<any>;
  @ContentChild('noData') noDataTemplate!: TemplateRef<any>;
  @ViewChild('.jump-bar') jumpBar!: ElementRef<HTMLDivElement>;
  @ViewChild('scroller') scroller!: CdkVirtualScrollViewport;

  @ViewChild(VirtualScrollerComponent) private virtualScroller!: VirtualScrollerComponent;

  filter!: SeriesFilter;
  libraries: Array<FilterItem<Library>> = [];

  updateApplied: number = 0;
  hasResumedJumpKey: boolean = false;

  private onDestory: Subject<void> = new Subject();

  get Breakpoint() {
    return Breakpoint;
  }

  constructor(private filterUtilitySerivce: FilterUtilitiesService, public utilityService: UtilityService,
    @Inject(DOCUMENT) private document: Document, private changeDetectionRef: ChangeDetectorRef,
    private jumpbarService: JumpbarService, private router: Router) {
    this.filter = this.filterUtilitySerivce.createSeriesFilter();
    this.changeDetectionRef.markForCheck();

  }

  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  resizeJumpBar() {
    const currentSize = (this.document.querySelector('.viewport-container')?.getBoundingClientRect().height || 10) - 30;
    this.jumpBarKeysToRender = this.jumpbarService.generateJumpBar(this.jumpBarKeys, currentSize);
    this.changeDetectionRef.markForCheck();
  }

  ngOnInit(): void {
    if (this.trackByIdentity === undefined) {
      this.trackByIdentity = (index: number, item: any) => `${this.header}_${this.updateApplied}_${item?.libraryId}`;
    }

    if (this.filterSettings === undefined) {
      this.filterSettings = new FilterSettings();
      this.changeDetectionRef.markForCheck();
    }

    if (this.pagination === undefined) {
      this.pagination = {currentPage: 1, itemsPerPage: this.items.length, totalItems: this.items.length, totalPages: 1};
      this.changeDetectionRef.markForCheck();
    }

    if (this.refresh) {
      this.refresh.subscribe(() => {
        this.changeDetectionRef.markForCheck();
        this.virtualScroller.refresh();
      });
    }
  }


  ngOnChanges(): void {
    this.jumpBarKeysToRender = [...this.jumpBarKeys];
    this.resizeJumpBar();
    
    // Don't resume jump key when there is a custom sort order, as it won't work
    if (this.hasCustomSort()) {
      if (!this.hasResumedJumpKey && this.jumpBarKeysToRender.length > 0) {
        const resumeKey = this.jumpbarService.getResumeKey(this.router.url);
        if (resumeKey === '') return;
        const keys = this.jumpBarKeysToRender.filter(k => k.key === resumeKey);
        if (keys.length < 1) return;
  
        this.hasResumedJumpKey = true;
        setTimeout(() => this.scrollTo(keys[0]), 100);
      }
    }
  }


  ngOnDestroy() {
    this.onDestory.next();
    this.onDestory.complete();
  }

  hasCustomSort() {
    return this.filter.sortOptions !== null || this.filterSettings?.presets?.sortOptions !== null;
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, undefined);
    }
  }

  applyMetadataFilter(event: FilterEvent) {
    this.applyFilter.emit(event);
    this.updateApplied++;
    this.changeDetectionRef.markForCheck();
  }


  scrollTo(jumpKey: JumpKey) {
    if (this.hasCustomSort()) return;

    let targetIndex = 0;
    for(var i = 0; i < this.jumpBarKeys.length; i++) {
      if (this.jumpBarKeys[i].key === jumpKey.key) break;
      targetIndex += this.jumpBarKeys[i].size;
    }

    this.virtualScroller.scrollToIndex(targetIndex, true, 0, 1000);
    this.jumpbarService.saveResumeKey(this.router.url, jumpKey.key);
    this.changeDetectionRef.markForCheck();
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
