import { CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, ContentChild, EventEmitter, Input, OnInit, Output, TemplateRef } from '@angular/core';

export interface IndexUpdateEvent {
  fromPosition: number;
  toPosition: number;
  item: any;
}

@Component({
  selector: 'app-dragable-ordered-list',
  templateUrl: './dragable-ordered-list.component.html',
  styleUrls: ['./dragable-ordered-list.component.scss']
})
export class DragableOrderedListComponent implements OnInit {

  @Input() items: Array<any> = [];
  @Output() orderUpdated: EventEmitter<IndexUpdateEvent> = new EventEmitter<IndexUpdateEvent>();
  @ContentChild('draggableItem') itemTemplate!: TemplateRef<any>;

  constructor() { }

  ngOnInit(): void {
  }

  drop(event: CdkDragDrop<string[]>) {
    moveItemInArray(this.items, event.previousIndex, event.currentIndex);
    this.orderUpdated.emit({
      fromPosition: event.previousIndex,
      toPosition: event.currentIndex,
      item: this.items[event.currentIndex]
    });
  }

  updateIndex(previousIndex: number, item: any) {
    // get the new value of the input 
    var inputElem = <HTMLInputElement>document.querySelector('#reorder-' + previousIndex);
    const newIndex = parseInt(inputElem.value, 10);
    moveItemInArray(this.items, previousIndex, newIndex);
    this.orderUpdated.emit({
      fromPosition: previousIndex,
      toPosition: newIndex,
      item: this.items[newIndex]
    });
  }

}
