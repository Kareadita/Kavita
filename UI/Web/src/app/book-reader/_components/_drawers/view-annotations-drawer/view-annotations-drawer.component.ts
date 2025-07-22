import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Signal} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {AnnotationCardComponent} from "../../_annotations/annotation-card/annotation-card.component";
import {Annotation} from "../../../_models/annotations/annotation";
import {AnnotationService} from "../../../../_services/annotation.service";
import {FilterPipe} from "../../../../_pipes/filter.pipe";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";

@Component({
  selector: 'app-view-annotations-drawer',
  imports: [
    TranslocoDirective,
    AnnotationCardComponent,
    FilterPipe,
    ReactiveFormsModule
  ],
  templateUrl: './view-annotations-drawer.component.html',
  styleUrl: './view-annotations-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ViewAnnotationsDrawerComponent {

  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly annotationService = inject(AnnotationService);

  annotations: Signal<Annotation[]> = this.annotationService.annotations;
  formGroup = new FormGroup({
    filter: new FormControl('', [])
  });
  readonly FilterAfter = 6;

  handleDelete(annotation: Annotation) {
    this.annotationService.delete(annotation.id).subscribe();
  }

  close() {
    this.activeOffcanvas.close();
  }

  filterList = (listItem: Annotation) => {
    const query = (this.formGroup.get('filter')?.value || '').toLowerCase();
    return listItem.comment.toLowerCase().indexOf(query) >= 0 || listItem.pageNumber.toString().indexOf(query) >= 0
      || (listItem.selectedText ?? '').toLowerCase().indexOf(query) >= 0;
  }
}
