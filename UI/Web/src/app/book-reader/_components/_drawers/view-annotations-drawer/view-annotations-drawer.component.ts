import {ChangeDetectionStrategy, ChangeDetectorRef, Component, effect, inject, model} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {AnnotationCardComponent} from "../../_annotations/annotation-card/annotation-card.component";
import {Annotation} from "../../../_models/annotation";
import {AnnotationService} from "../../../../_services/annotation.service";

@Component({
  selector: 'app-view-annotations-drawer',
  imports: [
    TranslocoDirective,
    AnnotationCardComponent
  ],
  templateUrl: './view-annotations-drawer.component.html',
  styleUrl: './view-annotations-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ViewAnnotationsDrawerComponent {

  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly annotationService = inject(AnnotationService);

  chapterId = model<number>(0);
  annotations: Annotation[] = [];

  constructor() {
    effect(() => {
      const chapterId = this.chapterId();
      if (chapterId === 0) return;

      this.annotationService.getAnnotations(chapterId).subscribe(annotations => {
        this.annotations = annotations;
        this.cdRef.markForCheck();
      })
    });
  }

  handleDelete(annotation: Annotation) {
    this.annotations.splice(this.annotations.indexOf(annotation), 1);
    this.cdRef.markForCheck();
  }



  close() {
    this.activeOffcanvas.close();
  }

}
