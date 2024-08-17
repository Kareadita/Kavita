import {ChangeDetectionStrategy, Component, Input} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ImageComponent} from "../../shared/image/image.component";
import {NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";

@Component({
  selector: 'app-external-list-item',
  standalone: true,
  imports: [CommonModule, ImageComponent, NgbProgressbar, NgbTooltip, ReadMoreComponent],
  templateUrl: './external-list-item.component.html',
  styleUrls: ['./external-list-item.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExternalListItemComponent {

  /**
   * Image to show
   */
  @Input() imageUrl: string = '';

  /**
   * Size of the Image Height. Defaults to 232.91px.
   */
  @Input() imageHeight: string = '232.91px';
  /**
   * Size of the Image Width Defaults to 160px.
   */
  @Input() imageWidth: string = '160px';
  @Input() summary: string | null = '';
}
