import {Component, input, model, output} from '@angular/core';
import {UtcToLocaleDatePipe} from "../../../_pipes/utc-to-locale-date.pipe";
import {DatePipe} from "@angular/common";

@Component({
  selector: 'app-annotation-card',
  imports: [
    UtcToLocaleDatePipe,
    DatePipe
  ],
  templateUrl: './annotation-card.component.html',
  styleUrl: './annotation-card.component.scss'
})
export class AnnotationCardComponent {
  position = input.required<any>();
  annotationText = input<string>('This is test text');
  createdDate = input<string>('01-01-0001');
  isHovered = model<boolean>(false);

  mouseEnter = output<void>();
  mouseLeave = output<void>();

  onMouseEnter() {
    this.mouseEnter.emit();
  }

  onMouseLeave() {
    this.mouseLeave.emit();
  }
}
