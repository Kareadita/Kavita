import { CdkVirtualScrollViewport } from '@angular/cdk/scrolling';
import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ContentChild, ElementRef, EventEmitter, HostListener, Inject, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, TemplateRef, TrackByFunction, ViewChild } from '@angular/core';
import { VirtualScrollerComponent } from '@iharbeck/ngx-virtual-scroller';
import { Subject } from 'rxjs';
import { FilterSettings } from 'src/app/metadata-filter/filter-settings';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { Library } from 'src/app/_models/library';
import { Pagination } from 'src/app/_models/pagination';
import { FilterEvent, FilterItem, SeriesFilter } from 'src/app/_models/series-filter';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { SeriesService } from 'src/app/_services/series.service';

const keySize = 24;

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


  @Input() jumpBarKeys: Array<JumpKey> = []; // This is aprox 784 pixels wide
  jumpBarKeysToRender: Array<JumpKey> = []; // Original

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

  private onDestory: Subject<void> = new Subject();

  get Breakpoint() {
    return Breakpoint;
  }

  constructor(private seriesService: SeriesService, public utilityService: UtilityService, 
    @Inject(DOCUMENT) private document: Document, private changeDetectionRef: ChangeDetectorRef) {
    this.filter = this.seriesService.createSeriesFilter();
    this.changeDetectionRef.markForCheck();
  }

  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  resizeJumpBar() {
    const fullSize = (this.jumpBarKeys.length * keySize);
    const currentSize = (this.document.querySelector('.viewport-container')?.getBoundingClientRect().height || 10) - 30;
    if (currentSize >= fullSize) {
      this.jumpBarKeysToRender = [...this.jumpBarKeys];
      this.changeDetectionRef.markForCheck();
      return;
    }

    const targetNumberOfKeys = parseInt(Math.floor(currentSize / keySize) + '', 10);
    const removeCount = this.jumpBarKeys.length - targetNumberOfKeys - 3;
    if (removeCount <= 0) return;


    this.jumpBarKeysToRender = [];
    
    const removalTimes = Math.ceil(removeCount / 2);
    const midPoint = Math.floor(this.jumpBarKeys.length / 2);
    this.jumpBarKeysToRender.push(this.jumpBarKeys[0]);
    this.removeFirstPartOfJumpBar(midPoint, removalTimes);
    this.jumpBarKeysToRender.push(this.jumpBarKeys[midPoint]);
    this.removeSecondPartOfJumpBar(midPoint, removalTimes);
    this.jumpBarKeysToRender.push(this.jumpBarKeys[this.jumpBarKeys.length - 1]);
    this.changeDetectionRef.markForCheck();
  }

  removeSecondPartOfJumpBar(midPoint: number, numberOfRemovals: number = 1) {
    const removedIndexes: Array<number> = [];
    for(let removal = 0; removal < numberOfRemovals; removal++) {
      let min = 100000000;
      let minIndex = -1;
      for(let i = midPoint + 1; i < this.jumpBarKeys.length - 2; i++) {
        if (this.jumpBarKeys[i].size < min && !removedIndexes.includes(i)) {
          min = this.jumpBarKeys[i].size;
          minIndex = i;
        }
      }
      removedIndexes.push(minIndex);
    }
    for(let i = midPoint + 1; i < this.jumpBarKeys.length - 2; i++) {
      if (!removedIndexes.includes(i)) this.jumpBarKeysToRender.push(this.jumpBarKeys[i]);
    }
  }

  removeFirstPartOfJumpBar(midPoint: number, numberOfRemovals: number = 1) {
    const removedIndexes: Array<number> = [];
    for(let removal = 0; removal < numberOfRemovals; removal++) {
      let min = 100000000;
      let minIndex = -1;
      for(let i = 1; i < midPoint; i++) {
        if (this.jumpBarKeys[i].size < min && !removedIndexes.includes(i)) {
          min = this.jumpBarKeys[i].size;
          minIndex = i;
        }
      }
      removedIndexes.push(minIndex);
    }

    for(let i = 1; i < midPoint; i++) {
      if (!removedIndexes.includes(i)) this.jumpBarKeysToRender.push(this.jumpBarKeys[i]);
    }
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
  }

  ngOnChanges(changes: SimpleChanges): void {
    this.jumpBarKeysToRender = [...this.jumpBarKeys];
    this.resizeJumpBar();
  }


  ngOnDestroy() {
    this.onDestory.next();
    this.onDestory.complete();
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, undefined);
    }
  }

  applyMetadataFilter(event: FilterEvent) {
    this.applyFilter.emit(event);
    this.updateApplied++;
    this.changeDetectionRef.markForCheck();
  }


  scrollTo(jumpKey: JumpKey) {
    let targetIndex = 0;
    for(var i = 0; i < this.jumpBarKeys.length; i++) {
      if (this.jumpBarKeys[i].key === jumpKey.key) break;
      targetIndex += this.jumpBarKeys[i].size;
    }

    this.virtualScroller.scrollToIndex(targetIndex, true, undefined, 1000);
    this.changeDetectionRef.markForCheck();
    return;
  }
}
