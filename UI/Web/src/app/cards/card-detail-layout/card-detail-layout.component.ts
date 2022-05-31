import { DOCUMENT } from '@angular/common';
import { AfterViewInit, Component, ContentChild, EventEmitter, HostListener, Inject, Input, OnDestroy, OnInit, Output, Renderer2, TemplateRef, ViewChild, ViewContainerRef } from '@angular/core';
import { from, Subject } from 'rxjs';
import { FilterSettings } from 'src/app/metadata-filter/filter-settings';
import { Breakpoint, UtilityService } from 'src/app/shared/_services/utility.service';
import { JumpKey } from 'src/app/_models/jumpbar/jump-key';
import { Library } from 'src/app/_models/library';
import { Pagination } from 'src/app/_models/pagination';
import { FilterEvent, FilterItem, SeriesFilter } from 'src/app/_models/series-filter';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { ScrollService } from 'src/app/_services/scroll.service';
import { SeriesService } from 'src/app/_services/series.service';

const FILTER_PAG_REGEX = /[^0-9]/g;

@Component({
  selector: 'app-card-detail-layout',
  templateUrl: './card-detail-layout.component.html',
  styleUrls: ['./card-detail-layout.component.scss']
})
export class CardDetailLayoutComponent implements OnInit, OnDestroy, AfterViewInit {

  @Input() header: string = '';
  @Input() isLoading: boolean = false;
  @Input() items: any[] = [];
  @Input() pagination!: Pagination;
  
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
  @Input() trackByIdentity!: (index: number, item: any) => string;
  @Input() filterSettings!: FilterSettings;


  @Input() jumpBarKeys: Array<JumpKey> = []; // This is aprox 784 pixels wide

  @Output() itemClicked: EventEmitter<any> = new EventEmitter();
  @Output() pageChange: EventEmitter<Pagination> = new EventEmitter();
  @Output() applyFilter: EventEmitter<FilterEvent> = new EventEmitter();

  @ContentChild('cardItem') itemTemplate!: TemplateRef<any>;
  @ContentChild('noData') noDataTemplate!: TemplateRef<any>;


  filter!: SeriesFilter;
  libraries: Array<FilterItem<Library>> = [];

  updateApplied: number = 0;

  intersectionObserver: IntersectionObserver = new IntersectionObserver((entries) => this.handleIntersection(entries), { threshold: 0.01 });


  private onDestory: Subject<void> = new Subject();

  get Breakpoint() {
    return Breakpoint;
  }

  constructor(private seriesService: SeriesService, public utilityService: UtilityService, @Inject(DOCUMENT) private document: Document,
              private scrollService: ScrollService) {
    this.filter = this.seriesService.createSeriesFilter();
  }

  @HostListener('window:resize', ['$event'])
  @HostListener('window:orientationchange', ['$event'])
  resizeJumpBar() {
    console.log('resizing jump bar');
    //const breakpoint = this.utilityService.getActiveBreakpoint();
    // if (window.innerWidth < 784) {
    //   // We need to remove a few sections of keys 
    //   const len = this.jumpBarKeys.length;
    //   if (this.jumpBarKeys.length <= 8) return;
    //   this.jumpBarKeys = this.jumpBarKeys.filter((item, index) => {
    //     return index % 2 === 0;
    //   });
    // }
  }

  ngOnInit(): void {
    this.trackByIdentity = (index: number, item: any) => `${this.header}_${this.pagination?.currentPage}_${this.updateApplied}_${item?.libraryId}`;


    if (this.filterSettings === undefined) {
      this.filterSettings = new FilterSettings();
    }

    if (this.pagination === undefined) {
      this.pagination = {currentPage: 1, itemsPerPage: this.items.length, totalItems: this.items.length, totalPages: 1}
    }
  }

  ngAfterViewInit() {

    const parent = this.document.querySelector('.card-container');
    if (parent == null) return;
    console.log('card divs', this.document.querySelectorAll('div[id^="jumpbar-index--"]'));
    console.log('cards: ', this.document.querySelectorAll('.card'));

    Array.from(this.document.querySelectorAll('div')).forEach(elem => this.intersectionObserver.observe(elem));
  }

  ngOnDestroy() {
    this.intersectionObserver.disconnect();
    this.onDestory.next();
    this.onDestory.complete();
  }

  handleIntersection(entries: IntersectionObserverEntry[]) {
    console.log('interception: ', entries.filter(e => e.target.hasAttribute('no-observe')));
    

  }

  onPageChange(page: number) {
    this.pageChange.emit(this.pagination);
  }

  selectPageStr(page: string) {
    this.pagination.currentPage = parseInt(page, 10) || 1;
    this.onPageChange(this.pagination.currentPage);
  }

  formatInput(input: HTMLInputElement) {
    input.value = input.value.replace(FILTER_PAG_REGEX, '');
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, undefined);
    }
  }

  applyMetadataFilter(event: FilterEvent) {
    this.applyFilter.emit(event);
    this.updateApplied++;
  }

  // onScroll() {

  // }

  // onScrollDown() {
  //   console.log('scrolled down');
  // }
  // onScrollUp() {
  //   console.log('scrolled up');
  // }

  

  scrollTo(jumpKey: JumpKey) {
    // TODO: Figure out how to do this
    
    let targetIndex = 0;
    for(var i = 0; i < this.jumpBarKeys.length; i++) {
      if (this.jumpBarKeys[i].key === jumpKey.key) break;
      targetIndex += this.jumpBarKeys[i].size;
    }
    //console.log('scrolling to card that starts with ', jumpKey.key, + ' with index of ', targetIndex);

    // Basic implementation based on itemsPerPage being the same. 
    //var minIndex = this.pagination.currentPage * this.pagination.itemsPerPage;
    var targetPage = Math.max(Math.ceil(targetIndex / this.pagination.itemsPerPage), 1);
    //console.log('We are on page ', this.pagination.currentPage, ' and our target page is ', targetPage);
    if (targetPage === this.pagination.currentPage) {
      // Scroll to the element
      const elem = this.document.querySelector(`div[id="jumpbar-index--${targetIndex}"`);
      if (elem !== null) {
        elem.scrollIntoView({
          behavior: 'smooth'
        });
      }
      return;
    }

    this.selectPageStr(targetPage + '');

    // if (minIndex > targetIndex) {
    //   // We need to scroll forward (potentially to another page)
    // } else if (minIndex < targetIndex) {
    //   // We need to scroll back (potentially to another page)
    // }
  }
}
