import {inject, Injectable, ViewContainerRef} from '@angular/core';
import {Annotation} from "../book-reader/_models/annotations/annotation";
import {EpubHighlightComponent} from "../book-reader/_components/_annotations/epub-highlight/epub-highlight.component";
import {DOCUMENT} from "@angular/common";

@Injectable({
  providedIn: 'root'
})
export class EpubHighlightService {

  private readonly document = inject(DOCUMENT);


  initializeHighlightElements(annotations: Annotation[], container: ViewContainerRef, selectFromElement?: Element | null | undefined,
                              configOptions: {showHighlight: boolean, showIcon: boolean} | null = null) {
    const annotationsMap: {[key: number]: Annotation} = annotations.reduce((map, obj) => {
      // @ts-ignore
      map[obj.id] = obj;
      return map;
    }, {});

    // Make the highlight components "real"
    const selector = selectFromElement ?? this.document;
    const highlightElems = selector.querySelectorAll('app-epub-highlight');

    for (let i = 0; i < highlightElems.length; i++) {
      const highlight = highlightElems[i];
      const idAttr = highlight.getAttribute('id');

      // Don't allow highlight injection unless the id is present
      if (!idAttr) continue;


      const annotationId = parseInt(idAttr.replace('epub-highlight-', ''), 10);
      const componentRef = container.createComponent<EpubHighlightComponent>(EpubHighlightComponent,
        {
          projectableNodes: [
            [document.createTextNode(highlight.innerHTML)]
          ]
        });

      if (highlight.parentNode != null) {
        highlight.parentNode.replaceChild(componentRef.location.nativeElement, highlight);
      }

      componentRef.setInput('annotation', annotationsMap[annotationId]);

      if (configOptions != null) {
        componentRef.setInput('showHighlight', configOptions.showHighlight);
      }
    }
  }
}
