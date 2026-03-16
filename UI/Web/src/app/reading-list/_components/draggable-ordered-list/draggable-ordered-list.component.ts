import {CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList, moveItemInArray} from '@angular/cdk/drag-drop';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  contentChild,
  inject,
  input,
  linkedSignal,
  output,
  TemplateRef,
  TrackByFunction
} from '@angular/core';
import {VirtualScrollerModule} from '@iharbeck/ngx-virtual-scroller';
import {NgClass, NgTemplateOutlet} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {BulkSelectionService} from "../../../cards/bulk-selection.service";
import {FormsModule} from "@angular/forms";

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
  imports: [VirtualScrollerModule, NgTemplateOutlet, CdkDropList, CdkDrag,
    CdkDragHandle, TranslocoDirective, NgClass, FormsModule]
})
export class DraggableOrderedListComponent {

  protected readonly bulkSelectionService = inject(BulkSelectionService);

  readonly items = input<Array<any>>([]);
  protected readonly localItems = linkedSignal(() => [...this.items()]);
  /**
   * After this many elements, drag and drop is disabled, and we use a virtualized list instead
   */
  virtualizeAfter = input(100);
  accessibilityMode = input(false);
  /**
   * Shows the remove button on the list item
   */
  showRemoveButton = input(true);
  /**
   * Parent scroll for virtualize pagination
   */
  parentScroll = input<Element | Window>();
  /**
   * Disables drag and drop functionality. Useful if a filter is present which will skew actual index.
   */
  disabled = input(false);
  /**
   * Disables remove button
   */
  disableRemove = input(false);
  /**
   * When enabled, draggability is disabled and a checkbox renders instead of order box or drag handle
   */
  bulkMode = input(false);
  trackByIdentity = input<TrackByFunction<any>>((index: number, item: any) => `${item.id}_${item.order}_${item.title}`);

  /**
   * After an item is re-ordered, you MUST reload from backend the new data. This is because accessibility mode will use item.order which needs to be in sync.
   */
  orderUpdated = output<IndexUpdateEvent>();
  itemRemove = output<ItemRemoveEvent>();

  itemTemplate = contentChild.required<TemplateRef<any>>('draggableItem');

  protected readonly bufferAmount = computed(() => Math.floor(Math.min(this.localItems().length / 20, 20)));
  protected readonly selectionSignal = this.bulkSelectionService.selectionSignal;

  drop(event: CdkDragDrop<string[]>) {
    if (event.previousIndex === event.currentIndex) return;
    this.localItems.update(arr => {
      moveItemInArray(arr, event.previousIndex, event.currentIndex);
      return [...arr];
    });
    this.orderUpdated.emit({
      fromPosition: event.previousIndex,
      toPosition: event.currentIndex,
      item: event.item.data,
      fromAccessibilityMode: false
    });
  }

  updateIndex(previousIndex: number, item: any) {
    const inputElem = document.querySelector<HTMLInputElement>('#reorder-' + previousIndex);
    if (!inputElem) return;
    const newIndex = parseInt(inputElem.value, 10);
    if (item.order === newIndex) return;
    this.localItems.update(arr => {
      moveItemInArray(arr, item.order, newIndex);
      return [...arr];
    });
    this.orderUpdated.emit({
      fromPosition: item.order,
      toPosition: newIndex,
      item,
      fromAccessibilityMode: true
    });
  }

  removeItem(item: any, position: number) {
    this.itemRemove.emit({
      position: item!.order,
      item
    });
  }

  selectItem(updatedVal: Event, index: number) {
    const boolVal = (updatedVal.target as HTMLInputElement).value == 'true';
    this.bulkSelectionService.handleCardSelection('sideNavStream', index, this.localItems().length, boolVal);
  }
}
