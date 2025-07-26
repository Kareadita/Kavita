import {Component, computed, effect, ElementRef, inject, input, model, ViewChild} from '@angular/core';
import {Annotation, HighlightColor} from "../../../_models/annotations/annotation";
import {EpubReaderMenuService} from "../../../../_services/epub-reader-menu.service";
import {AnnotationService} from "../../../../_services/annotation.service";
import {SlotColorPipe} from "../../../../_pipes/slot-color.pipe";
import {NgStyle} from "@angular/common";

@Component({
  selector: 'app-epub-highlight',
  imports: [
    NgStyle
  ],
  templateUrl: './epub-highlight.component.html',
  styleUrl: './epub-highlight.component.scss'
})
export class EpubHighlightComponent {
  private epubMenuService = inject(EpubReaderMenuService);
  private annotationService = inject(AnnotationService);

  showHighlight = model<boolean>(true);
  color = input<HighlightColor>(HighlightColor.Blue);

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

      //console.log('[highlight] annotation updated', annotation);

      this.annotation.set(annotations.filter(a => a.id === annotation.id)[0]);
    });
  }


  highlightStyle = computed(() => {
    const showHighlight = this.showHighlight();
    const annotation = this.annotation();
    const slots = this.annotationService.slots();

    if (!showHighlight || !annotation) {
      return '';
    }

    console.log('[highlight] slot updated', annotation);
    return this.highlightSlotPipe.transform(slots[annotation.selectedSlotIndex].color);
  });


  viewAnnotation() {
    // Don't view annotation if a drawer is already open
    if (this.epubMenuService.isDrawerOpen()) return;

    // TODO: This shouldn't when edit annotation drawer already open (clicking highlight in the drawer)
    this.epubMenuService.openViewAnnotationDrawer(this.annotation()!, false, (_) => {});
  }


  toggleHighlight() {
    this.showHighlight.set(!this.showHighlight());
  }

}
