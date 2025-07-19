import {Component, computed, ElementRef, inject, input, model, ViewChild} from '@angular/core';
import {Annotation, HighlightColor} from "../../../_models/annotation";
import {HighlightColorPipe} from "../../../../_pipes/highlight-color.pipe";
import {UtilityService} from "../../../../shared/_services/utility.service";
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
  private epubMenuService = inject(EpubReaderMenuService);
  private utilityService = inject(UtilityService);

  showHighlight = model<boolean>(true);
  color = input<HighlightColor>(HighlightColor.Blue);

  annotation = model.required<Annotation | null>();

  @ViewChild('highlightSpan', { static: false }) highlightSpan!: ElementRef;

  private readonly highlightColorPipe = new HighlightColorPipe();

  constructor() {

  }



  showAnnotationCard = computed(() => {
    const annotation = this.annotation();
    return this.showHighlight();
  });

  highlightClasses = computed(() => {
    const showHighlight = this.showHighlight();
    const annotation = this.annotation();

    if (!showHighlight || !annotation) {
      return '';
    }

    const colorClass = `epub-highlight epub-highlight-${this.highlightColorPipe.transform(annotation.highlightColor)}`;
    return `${colorClass}`;
  });


  viewAnnotation() {
    this.epubMenuService.openViewAnnotationDrawer(this.annotation()!, false, (_) => {

    });
  }


  toggleHighlight() {
    this.showHighlight.set(!this.showHighlight());
  }

}
