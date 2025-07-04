import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-view-annotation-drawer',
  imports: [
    TranslocoDirective
  ],
  templateUrl: './view-annotation-drawer.component.html',
  styleUrl: './view-annotation-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ViewAnnotationDrawerComponent {

  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly cdRef = inject(ChangeDetectorRef);


  close() {
    this.activeOffcanvas.close();
  }

}
