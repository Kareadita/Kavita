import { animate, state, style, transition, trigger } from '@angular/animations';
import { Component, ContentChild, EventEmitter, Input, OnInit, Output, TemplateRef } from '@angular/core';
import { FormGroup, FormControl } from '@angular/forms';
import { Pagination } from 'src/app/_models/pagination';
import { FilterItem } from 'src/app/_models/series-filter';
import { ActionItem } from 'src/app/_services/action-factory.service';

const FILTER_PAG_REGEX = /[^0-9]/g;

export enum FilterAction {
  /**
   * If an option is selected on a multi select component
   */
  Added = 0,
  /**
   * If an option is unselected on a multi select component
   */
  Removed = 1,
  /**
   * If an option is selected on a single select component
   */
  Selected = 2
}

export interface UpdateFilterEvent {
  filterItem: FilterItem;
  action: FilterAction;
}

const ANIMATION_SPEED = 300;

@Component({
  selector: 'app-card-detail-layout',
  templateUrl: './card-detail-layout.component.html',
  styleUrls: ['./card-detail-layout.component.scss']
})
export class CardDetailLayoutComponent implements OnInit {

  @Input() header: string = '';
  @Input() isLoading: boolean = false; 
  @Input() items: any[] = [];
  @Input() pagination!: Pagination;
  /**
   * Any actions to exist on the header for the parent collection (library, collection)
   */
  @Input() actions: ActionItem<any>[] = [];
  /**
   * A list of Filters which can filter the data of the page. If nothing is passed, the control will not show.
   */
  @Input() filters: Array<FilterItem> = [];
  @Input() trackByIdentity!: (index: number, item: any) => string;
  @Output() itemClicked: EventEmitter<any> = new EventEmitter();
  @Output() pageChange: EventEmitter<Pagination> = new EventEmitter();
  @Output() applyFilter: EventEmitter<UpdateFilterEvent> = new EventEmitter();
  
  @ContentChild('cardItem') itemTemplate!: TemplateRef<any>;
  
  filterForm: FormGroup = new FormGroup({
    filter: new FormControl(0, []),
  });

  /**
   * Controls the visiblity of extended controls that sit below the main header.
   */
  filteringCollapsed: boolean = true;

  constructor() { }

  ngOnInit(): void {
    this.trackByIdentity = (index: number, item: any) => `${this.header}_${this.pagination?.currentPage}_${index}`;
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

  handleFilterChange(index: string) {
    this.applyFilter.emit({
      filterItem: this.filters[parseInt(index, 10)],
      action: FilterAction.Selected
    });

  }

}
