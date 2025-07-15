import {Component, input, model, output} from '@angular/core';

@Component({
  selector: 'app-annotation-card',
  imports: [],
  templateUrl: './annotation-card.component.html',
  styleUrl: './annotation-card.component.scss'
})
export class AnnotationCardComponent {
  annotation = input.required()
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
