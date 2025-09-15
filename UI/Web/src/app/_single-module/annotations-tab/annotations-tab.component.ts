import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {Annotation} from "../../book-reader/_models/annotations/annotation";
import {
  AnnotationCardComponent
} from "../../book-reader/_components/_annotations/annotation-card/annotation-card.component";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";

@Component({
  selector: 'app-annotations-tab',
  imports: [
    TranslocoDirective,
    AnnotationCardComponent,
    VirtualScrollerModule
  ],
  templateUrl: './annotations-tab.component.html',
  styleUrl: './annotations-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AnnotationsTabComponent {

  annotations = input.required<Annotation[]>();
  scrollingBlock = input.required<HTMLDivElement>();
  displaySeries = input<boolean>(false);

}
