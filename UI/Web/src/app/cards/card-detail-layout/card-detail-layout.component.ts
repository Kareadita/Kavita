import { Component, ContentChild, EventEmitter, Input, OnDestroy, OnInit, Output, TemplateRef } from '@angular/core';
import { Subject } from 'rxjs';
import { FilterSettings } from 'src/app/metadata-filter/filter-settings';
import { Library } from 'src/app/_models/library';
import { Pagination } from 'src/app/_models/pagination';
import { FilterEvent, FilterItem, SeriesFilter, SortField } from 'src/app/_models/series-filter';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { SeriesService } from 'src/app/_services/series.service';

const FILTER_PAG_REGEX = /[^0-9]/g;

const ANIMATION_SPEED = 300;


@Component({
  selector: 'app-card-detail-layout',
  templateUrl: './card-detail-layout.component.html',
  styleUrls: ['./card-detail-layout.component.scss']
})
export class CardDetailLayoutComponent implements OnInit, OnDestroy {

  @Input() header: string = '';
  @Input() isLoading: boolean = false;
  @Input() items: any[] = [];
  @Input() pagination!: Pagination;
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
  @Output() itemClicked: EventEmitter<any> = new EventEmitter();
  @Output() pageChange: EventEmitter<Pagination> = new EventEmitter();
  @Output() applyFilter: EventEmitter<FilterEvent> = new EventEmitter();

  @ContentChild('cardItem') itemTemplate!: TemplateRef<any>;


  // Filter Code
  @Input() filterOpen!: EventEmitter<boolean>;


  filter!: SeriesFilter;
  libraries: Array<FilterItem<Library>> = [];

  updateApplied: number = 0;


  private onDestory: Subject<void> = new Subject();

  constructor(private seriesService: SeriesService) {
    this.filter = this.seriesService.createSeriesFilter();
  }

  ngOnInit(): void {
    this.trackByIdentity = (index: number, item: any) => `${this.header}_${this.pagination?.currentPage}_${this.updateApplied}_${item?.libraryId}`;


    if (this.filterSettings === undefined) {
      this.filterSettings = new FilterSettings();
    }
  }

  ngOnDestroy() {
    this.onDestory.next();
    this.onDestory.complete();
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

}
