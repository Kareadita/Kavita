import {ChangeDetectionStrategy, Component, input, output} from '@angular/core';
import {NgClass} from "@angular/common";

@Component({
  selector: 'app-icon-and-title',
  imports: [
    NgClass
  ],
  templateUrl: './icon-and-title.component.html',
  styleUrls: ['./icon-and-title.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class IconAndTitleComponent {
  /**
   * If the component is clickable and should emit click events
   */
  clickable = input<boolean>(true);
  title = input<string>('');
  label = input<string>('');
  /**
   * Font classes used to display font
   */
  fontClasses = input<string>('');

  clicked = output<MouseEvent>();

  constructor() { }

  handleClick(event: MouseEvent) {
    if (this.clickable()) this.clicked.emit(event);
  }
}
