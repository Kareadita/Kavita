import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import {CommonModule} from "@angular/common";

@Component({
  selector: 'app-icon-and-title',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './icon-and-title.component.html',
  styleUrls: ['./icon-and-title.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IconAndTitleComponent {
  /**
   * If the component is clickable and should emit click events
   */
  @Input() clickable: boolean = true;
  @Input() title: string = '';
  @Input() label: string = '';
  /**
   * Font classes used to display font
   */
  @Input() fontClasses: string = '';

  @Output() click: EventEmitter<MouseEvent> = new EventEmitter<MouseEvent>();

  constructor() { }

  handleClick(event: MouseEvent) {
    if (this.clickable) this.click.emit(event);
  }
}
