import { CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ContentChild, EventEmitter, Input, Output, TemplateRef } from '@angular/core';

export interface IndexUpdateEvent {
  fromPosition: number;
  toPosition: number;
  item: any;
}

export interface ItemRemoveEvent {
  position: number;
  item: any;
}

@Component({
  selector: 'app-draggable-ordered-list',
  templateUrl: './draggable-ordered-list.component.html',
  styleUrls: ['./draggable-ordered-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DraggableOrderedListComponent {

  @Input() accessibilityMode: boolean = false;
  /**
   * Shows the remove button on the list item
   */
  @Input() showRemoveButton: boolean = true;
  @Input() items: Array<any> = [];
  @Output() orderUpdated: EventEmitter<IndexUpdateEvent> = new EventEmitter<IndexUpdateEvent>();
  @Output() itemRemove: EventEmitter<ItemRemoveEvent> = new EventEmitter<ItemRemoveEvent>();
  @ContentChild('draggableItem') itemTemplate!: TemplateRef<any>;

  constructor(private readonly cdRef: ChangeDetectorRef) { }

  drop(event: CdkDragDrop<string[]>) {
    if (event.previousIndex === event.currentIndex)  return;
    moveItemInArray(this.items, event.previousIndex, event.currentIndex);
    this.orderUpdated.emit({
      fromPosition: event.previousIndex,
      toPosition: event.currentIndex,
      item: this.items[event.currentIndex]
    });
    this.cdRef.markForCheck();
  }

  updateIndex(previousIndex: number, item: any) {
    // get the new value of the input 
    var inputElem = <HTMLInputElement>document.querySelector('#reorder-' + previousIndex);
    const newIndex = parseInt(inputElem.value, 10);
    if (previousIndex === newIndex)  return;
    moveItemInArray(this.items, previousIndex, newIndex);
    this.orderUpdated.emit({
      fromPosition: previousIndex,
      toPosition: newIndex,
      item: this.items[newIndex]
    });
    this.cdRef.markForCheck();
  }

  removeItem(item: any, position: number) {
    this.itemRemove.emit({
      position,
      item
    });
    this.cdRef.markForCheck();
  }
}
