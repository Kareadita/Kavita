import { CdkDragDrop, moveItemInArray, CdkDropList, CdkDrag, CdkDragHandle } from '@angular/cdk/drag-drop';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild,
  EventEmitter,
  inject,
  Input,
  Output,
  TemplateRef,
  TrackByFunction
} from '@angular/core';
import { VirtualScrollerModule } from '@iharbeck/ngx-virtual-scroller';
import {NgIf, NgFor, NgTemplateOutlet, NgClass} from '@angular/common';
import {TranslocoDirective} from "@ngneat/transloco";
import {BulkSelectionService} from "../../../cards/bulk-selection.service";
import {SeriesCardComponent} from "../../../cards/series-card/series-card.component";

export interface IndexUpdateEvent {
  fromPosition: number;
  toPosition: number;
  item: any;
  fromAccessibilityMode: boolean;
}

export interface ItemRemoveEvent {
  position: number;
  item: any;
}

@Component({
    selector: 'app-draggable-ordered-list',
    templateUrl: './draggable-ordered-list.component.html',
    styleUrls: ['./draggable-ordered-list.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgIf, VirtualScrollerModule, NgFor, NgTemplateOutlet, CdkDropList, CdkDrag, CdkDragHandle, TranslocoDirective, NgClass, SeriesCardComponent]
})
export class DraggableOrderedListComponent {

  @Input() accessibilityMode: boolean = false;
  /**
   * Shows the remove button on the list item
   */
  @Input() showRemoveButton: boolean = true;
  @Input() items: Array<any> = [];
  /**
   * Parent scroll for virtualize pagination
   */
  @Input() parentScroll!: Element | Window;
  /**
   * Disables drag and drop functionality. Useful if a filter is present which will skew actual index.
   */
  @Input() disabled: boolean = false;
  /**
   * When enabled, draggability is disabled and a checkbox renders instead of order box or drag handle
   */
  @Input() bulkMode: boolean = false;
  @Input() trackByIdentity: TrackByFunction<any> = (index: number, item: any) => `${item.id}_${item.order}_${item.title}`;
  @Output() orderUpdated: EventEmitter<IndexUpdateEvent> = new EventEmitter<IndexUpdateEvent>();
  @Output() itemRemove: EventEmitter<ItemRemoveEvent> = new EventEmitter<ItemRemoveEvent>();
  @ContentChild('draggableItem') itemTemplate!: TemplateRef<any>;

  public readonly bulkSelectionService = inject(BulkSelectionService);

  get BufferAmount() {
    return Math.min(this.items.length / 20, 20);
  }

  constructor(private readonly cdRef: ChangeDetectorRef) { }

  drop(event: CdkDragDrop<string[]>) {
    if (event.previousIndex === event.currentIndex)  return;
    moveItemInArray(this.items, event.previousIndex, event.currentIndex);
    this.orderUpdated.emit({
      fromPosition: event.previousIndex,
      toPosition: event.currentIndex,
      item: this.items[event.currentIndex],
      fromAccessibilityMode: false
    });
    this.cdRef.markForCheck();
  }

  updateIndex(previousIndex: number, item: any) {
    // get the new value of the input
    const inputElem = <HTMLInputElement>document.querySelector('#reorder-' + previousIndex);
    const newIndex = parseInt(inputElem.value, 10);
    if (previousIndex === newIndex)  return;
    moveItemInArray(this.items, previousIndex, newIndex);
    this.orderUpdated.emit({
      fromPosition: previousIndex,
      toPosition: newIndex,
      item: this.items[newIndex],
      fromAccessibilityMode: true
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

  selectItem(updatedVal: any, index: number) {
    console.log('select item event: ', updatedVal);
    this.bulkSelectionService.handleCardSelection('sideNavStream', index, this.items.length, updatedVal);
    this.cdRef.markForCheck();
  }
}
