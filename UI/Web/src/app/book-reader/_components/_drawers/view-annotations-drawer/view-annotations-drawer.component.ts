import {ChangeDetectionStrategy, Component, inject, output, signal, Signal} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {AnnotationCardComponent} from "../../_annotations/annotation-card/annotation-card.component";
import {Annotation} from "../../../_models/annotations/annotation";
import {AnnotationService} from "../../../../_services/annotation.service";
import {
  OffCanvasResizeComponent,
  ResizeMode
} from "../../../../shared/_components/off-canvas-resize/off-canvas-resize.component";
import {AccountService} from "../../../../_services/account.service";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {FilterFieldComponent} from "../../../../shared/_components/filter-field/filter-field.component";
import {filteredBy} from "../../../../_helpers/filtered";

@Component({
  selector: 'app-view-annotations-drawer',
  imports: [
    TranslocoDirective,
    AnnotationCardComponent,
    OffCanvasResizeComponent,
    VirtualScrollerModule,
    FilterFieldComponent
  ],
  templateUrl: './view-annotations-drawer.component.html',
  styleUrl: './view-annotations-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ViewAnnotationsDrawerComponent {

  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly annotationService = inject(AnnotationService);
  protected readonly accountService = inject(AccountService);

  readonly loadAnnotation = output<Annotation>();

  annotations: Signal<Annotation[]> = this.annotationService.annotations;
  filterQuery = signal('');
  protected readonly filteredAnnotationsList = filteredBy(this.annotations, this.filterQuery,
    'comment', 'pageNumber', 'selectedText');
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

  protected readonly window = window;
  protected readonly ResizeMode = ResizeMode;
}
