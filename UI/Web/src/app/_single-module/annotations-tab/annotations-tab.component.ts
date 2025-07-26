import {Component, input} from '@angular/core';
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {Annotation} from "../../book-reader/_models/annotations/annotation";
import {
  AnnotationCardComponent
} from "../../book-reader/_components/_annotations/annotation-card/annotation-card.component";

@Component({
  selector: 'app-annotations-tab',
  imports: [
    CarouselReelComponent,
    TranslocoDirective,
    AnnotationCardComponent
  ],
  templateUrl: './annotations-tab.component.html',
  styleUrl: './annotations-tab.component.scss'
})
export class AnnotationsTabComponent {

  annotations = input.required<Annotation[]>();

}
