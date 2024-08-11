import { CdkDragDrop, moveItemInArray, CdkDropList, CdkDrag, CdkDragHandle } from '@angular/cdk/drag-drop';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild, DestroyRef,
  EventEmitter,
  inject,
  Input,
  Output,
  TemplateRef,
  TrackByFunction
} from '@angular/core';
import { VirtualScrollerModule } from '@iharbeck/ngx-virtual-scroller';
import {NgIf, NgFor, NgTemplateOutlet, NgClass} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {BulkSelectionService} from "../../../cards/bulk-selection.service";
import {SeriesCardComponent} from "../../../cards/series-card/series-card.component";
import {FormsModule} from "@angular/forms";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

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
  imports: [NgIf, VirtualScrollerModule, NgFor, NgTemplateOutlet, CdkDropList, CdkDrag,
    CdkDragHandle, TranslocoDirective, NgClass, SeriesCardComponent, FormsModule]
})
export class DraggableOrderedListComponent {

  /**
   * After this many elements, drag and drop is disabled and we use a virtualized list instead
   */
  @Input() virtualizeAfter = 100;
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
  /**
   * After an item is re-ordered, you MUST reload from backend the new data. This is because accessibility mode will use item.order which needs to be in sync.
   */
  @Output() orderUpdated: EventEmitter<IndexUpdateEvent> = new EventEmitter<IndexUpdateEvent>();
  @Output() itemRemove: EventEmitter<ItemRemoveEvent> = new EventEmitter<ItemRemoveEvent>();
  @ContentChild('draggableItem') itemTemplate!: TemplateRef<any>;

  public readonly bulkSelectionService = inject(BulkSelectionService);
  public readonly destroyRef = inject(DestroyRef);

  get BufferAmount() {
    return Math.min(this.items.length / 20, 20);
  }

  constructor(private readonly cdRef: ChangeDetectorRef) {
    this.bulkSelectionService.selections$.pipe(
        takeUntilDestroyed(this.destroyRef)
    ).subscribe((s) => {
      this.cdRef.markForCheck()
    });
  }

  drop(event: CdkDragDrop<string[]>) {
    if (event.previousIndex === event.currentIndex) return;
    moveItemInArray(this.items, event.previousIndex, event.currentIndex);
    this.orderUpdated.emit({
      fromPosition: event.previousIndex,
      toPosition: event.currentIndex,
      item: event.item.data,
      fromAccessibilityMode: false
    });
    this.cdRef.markForCheck();
  }

  updateIndex(previousIndex: number, item: any) {
    // get the new value of the input
    const inputElem = <HTMLInputElement>document.querySelector('#reorder-' + previousIndex);
    const newIndex = parseInt(inputElem.value, 10);
    if (item.order === newIndex) return;
    moveItemInArray(this.items, item.order, newIndex);
    this.orderUpdated.emit({
      fromPosition: item.order,
      toPosition: newIndex,
      item: item,
      fromAccessibilityMode: true
    });
    this.cdRef.markForCheck();
  }

  removeItem(item: any, position: number) {
    this.itemRemove.emit({
      position: item!.order,
      item
    });
    this.cdRef.markForCheck();
  }

  selectItem(updatedVal: Event, index: number) {
    const boolVal = (updatedVal.target as HTMLInputElement).value == 'true';

    this.bulkSelectionService.handleCardSelection('sideNavStream', index, this.items.length, boolVal);
    this.cdRef.markForCheck();
  }
}
