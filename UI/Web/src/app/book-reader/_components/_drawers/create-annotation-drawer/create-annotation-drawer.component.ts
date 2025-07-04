import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, model} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {CreateAnnotationRequest} from "../../../_models/create-annotation-request";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-create-annotation-drawer',
  imports: [
    TranslocoDirective
  ],
  templateUrl: './create-annotation-drawer.component.html',
  styleUrl: './create-annotation-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateAnnotationDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly cdRef = inject(ChangeDetectorRef);

  createAnnotation = model<CreateAnnotationRequest | null>(null);


  close() {
    this.activeOffcanvas.close();
  }
}
