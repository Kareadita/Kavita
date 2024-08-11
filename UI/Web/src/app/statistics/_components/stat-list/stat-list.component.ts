import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { Observable } from 'rxjs';
import { PieDataItem } from '../../_models/pie-data-item';
import { CompactNumberPipe } from '../../../_pipes/compact-number.pipe';
import { ImageComponent } from '../../../shared/image/image.component';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { NgClass, AsyncPipe } from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
    selector: 'app-stat-list',
    templateUrl: './stat-list.component.html',
    styleUrls: ['./stat-list.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [NgbTooltip, NgClass, ImageComponent, AsyncPipe, CompactNumberPipe, TranslocoDirective]
})
export class StatListComponent {

  /**
   * Title of list
   */
  @Input() title: string = ''
  /**
   * Optional label to render after value
   */
  @Input() label: string = ''
  /**
   * Optional data to put in tooltip
   */
  @Input() description: string = '';
  @Input({required: true}) data$!: Observable<PieDataItem[]>;
  @Input() image: ((data: PieDataItem) => string) | undefined = undefined;
  /**
   * Optional callback handler when an item is clicked
   */
  @Input() handleClick: ((data: PieDataItem) => void) | undefined = undefined;

  doClick(item: PieDataItem) {
    if (!this.handleClick) return;
    this.handleClick(item);
  }

}
