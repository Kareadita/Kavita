import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-view-toc-drawer',
  imports: [
    TranslocoDirective
  ],
  templateUrl: './view-toc-drawer.component.html',
  styleUrl: './view-toc-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ViewTocDrawerComponent {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly cdRef = inject(ChangeDetectorRef);


  close() {
    this.activeOffcanvas.close();
  }
}
