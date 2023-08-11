import { CdkVirtualScrollViewport } from '@angular/cdk/scrolling';
import {CommonModule, DOCUMENT} from '@angular/common';
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
  OnDestroy,
  OnInit,
  Output,
  TemplateRef,
  TrackByFunction,
  ViewChild
} from '@angular/core';
import { Router } from '@angular/router';
import {VirtualScrollerComponent, VirtualScrollerModule} from '@iharbeck/ngx-virtual-scroller';
import { FilterSettings } from 'src/app/metadata-filter/filter-settings';
import { FilterUtilitiesService } from 'src/app/shared/_services/filter-utilities.service';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { Library } from 'src/app/_models/library';
import { Pagination } from 'src/app/_models/pagination';
import { FilterEvent, FilterItem, SeriesFilter } from 'src/app/_models/metadata/series-filter';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { JumpbarService } from 'src/app/_services/jumpbar.service';
import { ScrollService } from 'src/app/_services/scroll.service';
import {LoadingComponent} from "../../shared/loading/loading.component";


import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {MetadataFilterComponent} from "../../metadata-filter/metadata-filter.component";
import {TranslocoDirective} from "@ngneat/transloco";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {SeriesFilterV2} from "../../_models/metadata/v2/series-filter-v2";

@Component({
  selector: 'app-card-detail-layout',
  standalone: true,
  imports: [CommonModule, LoadingComponent, VirtualScrollerModule, CardActionablesComponent, NgbTooltip, MetadataFilterComponent, TranslocoDirective],
  templateUrl: './card-detail-layout.component.html',
  styleUrls: ['./card-detail-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CardDetailLayoutComponent implements OnInit, OnChanges {

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

  private readonly filterUtilityService = inject(FilterUtilitiesService);
  filter: SeriesFilterV2 = this.filterUtilityService.createSeriesV2Filter();
  libraries: Array<FilterItem<Library>> = [];

  updateApplied: number = 0;
  hasResumedJumpKey: boolean = false;


  get Breakpoint() {
    return Breakpoint;
  }

  constructor(public utilityService: UtilityService,
    @Inject(DOCUMENT) private document: Document, private cdRef: ChangeDetectorRef,
    private jumpbarService: JumpbarService, private router: Router, private scrollService: ScrollService) {
  }

  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  resizeJumpBar() {
    const currentSize = (this.document.querySelector('.viewport-container')?.getBoundingClientRect().height || 10) - 30;
    this.jumpBarKeysToRender = this.jumpbarService.generateJumpBar(this.jumpBarKeys, currentSize);
    this.cdRef.markForCheck();
  }

  ngOnInit(): void {
    if (this.trackByIdentity === undefined) {
      this.trackByIdentity = (index: number, item: any) => `${this.header}_${this.updateApplied}_${item?.libraryId}`;
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
    } else {
      // I will come back and refactor this to work
      // const scrollPosition = this.jumpbarService.getResumePosition(this.router.url);
      // console.log('scroll position: ', scrollPosition);
      // if (scrollPosition > 0) {
      //   setTimeout(() => this.virtualScroller.scrollToIndex(scrollPosition, true, 0, 1000), 100);
      // }
    }
  }

  hasCustomSort() {
    return this.filter?.sortOptions || this.filterSettings?.presetsV2?.sortOptions;
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, undefined);
    }
  }

  applyMetadataFilter(event: FilterEvent) {
    console.log('card-detail-layout applying metadata filter: ', event);
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

    this.virtualScroller.scrollToIndex(targetIndex, true, 0, 1000);
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
