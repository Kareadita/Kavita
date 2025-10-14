import {Component, computed, DestroyRef, effect, ElementRef, inject, input, model, ViewChild} from '@angular/core';
import {Annotation} from "../../../_models/annotations/annotation";
import {EpubReaderMenuService} from "../../../../_services/epub-reader-menu.service";
import {AnnotationService} from "../../../../_services/annotation.service";
import {SlotColorPipe} from "../../../../_pipes/slot-color.pipe";
import {NgStyle} from "@angular/common";
import {MessageHubService} from "../../../../_services/message-hub.service";

@Component({
  selector: 'app-epub-highlight',
  imports: [
    NgStyle
  ],
  templateUrl: './epub-highlight.component.html',
  styleUrl: './epub-highlight.component.scss'
})
export class EpubHighlightComponent {
  private readonly epubMenuService = inject(EpubReaderMenuService);
  private readonly annotationService = inject(AnnotationService);
  private readonly messageHub = inject(MessageHubService);
  private readonly destroyRef = inject(DestroyRef);

  showHighlight = model<boolean>(true);

  annotation = model.required<Annotation | null>();

  @ViewChild('highlightSpan', { static: false }) highlightSpan!: ElementRef;

  private readonly highlightSlotPipe = new SlotColorPipe();

  constructor() {

    effect(() => {
      const updateEvent = this.annotationService.events();
      const annotation = this.annotation();
      const annotations = this.annotationService.annotations();

      if (!updateEvent || !annotation || updateEvent.annotation.id !== annotation.id) return;
      if (updateEvent.type !== 'edit') return;

      this.annotation.set(annotations.filter(a => a.id === annotation.id)[0]);
    });
  }


  highlightStyle = computed(() => {
    const showHighlight = this.showHighlight();
    const annotation = this.annotation();
    const slots = this.annotationService.slots();

    if (!showHighlight || !annotation || slots.length === 0 || slots.length < annotation.selectedSlotIndex) {
      return '';
    }

    return this.highlightSlotPipe.transform(slots[annotation.selectedSlotIndex].color);
  });


  viewAnnotation() {
    // Don't view annotation if a drawer is already open
    if (this.epubMenuService.isDrawerOpen()) return;

    this.epubMenuService.openViewAnnotationDrawer(this.annotation()!, false, (_) => {});
  }

}
