import {Component, computed, ElementRef, inject, input, model, signal, ViewChild} from '@angular/core';
import {Annotation, HighlightColor} from "../../../_models/annotation";
import {AnnotationCardService} from 'src/app/_service/annotation-card.service';
import {HighlightColorPipe} from "../../../../_pipes/highlight-color.pipe";
import {UserBreakpoint, UtilityService} from "../../../../shared/_services/utility.service";
import {EpubReaderMenuService} from "../../../../_services/epub-reader-menu.service";

@Component({
  selector: 'app-epub-highlight',
  imports: [
    HighlightColorPipe
  ],
  templateUrl: './epub-highlight.component.html',
  styleUrl: './epub-highlight.component.scss'
})
export class EpubHighlightComponent {
  private annotationCardService = inject(AnnotationCardService);
  private epubMenuService = inject(EpubReaderMenuService);
  private utilityService = inject(UtilityService);

  showHighlight = model<boolean>(false);
  color = input<HighlightColor>(HighlightColor.Blue);
  annotation = model.required<Annotation | null>();
  isHovered = signal<boolean>(false);
  showIcon = model<boolean>(true);

  @ViewChild('highlightSpan', { static: false }) highlightSpan!: ElementRef;

  private resizeObserver?: ResizeObserver;
  private readonly highlightColorPipe = new HighlightColorPipe();

  constructor() {

  }



  showAnnotationCard = computed(() => {
    const annotation = this.annotation();
    return this.showHighlight();
  });

  highlightClasses = computed(() => {
    if (!this.showHighlight() || !this.annotation()) {
      return '';
    }

    const colorClass = `epub-highlight-${this.highlightColorPipe.transform(this.annotation()!.highlightColor)}`;
    return `${colorClass}`;
  });

  viewAnnotation() {
    if (this.utilityService.activeUserBreakpoint() <= UserBreakpoint.Tablet) {
      // Open a modal to view the annotation?
    }

    // this.epubMenuService.openViewAnnotationDrawer(this.annotation(), () => {
    //
    // });
  }


  onMouseEnter() {
    this.isHovered.set(true);
    if (this.annotation() && this.showAnnotationCard()) {
      //this.showAnnotationCard.update(true);
    }
  }

  onMouseLeave() {
    this.isHovered.set(false);
    //this.showAnnotationCard.set(false);
  }


  toggleHighlight() {
    this.showHighlight.set(!this.showHighlight());
  }

}
