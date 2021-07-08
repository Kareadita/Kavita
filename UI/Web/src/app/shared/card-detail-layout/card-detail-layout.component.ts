import { Component, ContentChild, EventEmitter, Input, OnInit, Output, TemplateRef } from '@angular/core';
import { Pagination } from 'src/app/_models/pagination';
import { ActionItem } from 'src/app/_services/action-factory.service';

const FILTER_PAG_REGEX = /[^0-9]/g;

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
  @Input() actions: ActionItem<any>[] = [];
  @Input() trackByIdentity!: (index: number, item: any) => string;
  @Output() itemClicked: EventEmitter<any> = new EventEmitter();
  @Output() pageChange: EventEmitter<Pagination> = new EventEmitter();
  
  @ContentChild('cardItem') itemTemplate!: TemplateRef<any>;

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

}
