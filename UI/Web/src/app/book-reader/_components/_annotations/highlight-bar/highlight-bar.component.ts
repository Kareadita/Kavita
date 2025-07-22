import {Component, computed, EventEmitter, inject, model, Output} from '@angular/core';
import {NgClass, NgStyle} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {HighlightSlot} from "../../../_models/annotations/highlight-slot";
import {SlotColorPipe} from "../../../../_pipes/slot-color.pipe";
import {AnnotationService} from "../../../../_services/annotation.service";

@Component({
  selector: 'app-highlight-bar',
  imports: [
    NgClass,
    NgStyle,
    TranslocoDirective,
    SlotColorPipe
  ],
  templateUrl: './highlight-bar.component.html',
  styleUrl: './highlight-bar.component.scss'
})
export class HighlightBarComponent {

  private readonly annotationService = inject(AnnotationService);

  selectedSlotIndex = model.required<number>();
  @Output() changeSlot = new EventEmitter<number>();
  slots = this.annotationService.slots;

  selectedSlot = computed(() => {
    const index = this.selectedSlotIndex();
    const slots = this.annotationService.slots();
    if (slots.length === 0 || index >= slots.length) return null;
    return slots[index];
  })

  selectSlot(index: number, slot: HighlightSlot) {
    this.selectedSlotIndex.set(index);
    this.changeSlot.emit(index);
  }
}
