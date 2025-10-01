import {ChangeDetectionStrategy, Component, EventEmitter, inject, Output, Signal} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {AnnotationCardComponent} from "../../_annotations/annotation-card/annotation-card.component";
import {Annotation} from "../../../_models/annotations/annotation";
import {AnnotationService} from "../../../../_services/annotation.service";
import {FilterPipe} from "../../../../_pipes/filter.pipe";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {
  OffCanvasResizeComponent,
  ResizeMode
} from "../../../../shared/_components/off-canvas-resize/off-canvas-resize.component";
import {AccountService} from "../../../../_services/account.service";

@Component({
  selector: 'app-view-annotations-drawer',
  imports: [
    TranslocoDirective,
    AnnotationCardComponent,
    FilterPipe,
    ReactiveFormsModule,
    OffCanvasResizeComponent
  ],
  templateUrl: './view-annotations-drawer.component.html',
  styleUrl: './view-annotations-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ViewAnnotationsDrawerComponent {

  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly annotationService = inject(AnnotationService);
  protected readonly accountService = inject(AccountService);

  @Output() loadAnnotation: EventEmitter<Annotation> = new EventEmitter();

  annotations: Signal<Annotation[]> = this.annotationService.annotations;
  formGroup = new FormGroup({
    filter: new FormControl('', [])
  });
  readonly FilterAfter = 4;

  handleDelete(annotation: Annotation) {
    this.annotationService.delete(annotation.id).subscribe();
  }

  handleNavigateTo(annotation: Annotation) {
    this.loadAnnotation.emit(annotation);
    this.close();
  }

  close() {
    this.activeOffcanvas.close();
  }

  filterList = (listItem: Annotation) => {
    const query = (this.formGroup.get('filter')?.value || '').toLowerCase();
    return listItem.comment.toLowerCase().indexOf(query) >= 0 || listItem.pageNumber.toString().indexOf(query) >= 0
      || (listItem.selectedText ?? '').toLowerCase().indexOf(query) >= 0;
  }

  protected readonly window = window;
  protected readonly ResizeMode = ResizeMode;
}
