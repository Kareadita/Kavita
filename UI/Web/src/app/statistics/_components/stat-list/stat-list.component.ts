import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { PieData } from '@swimlane/ngx-charts';
import { Observable } from 'rxjs';
import { PieDataItem } from '../../_models/pie-data-item';

@Component({
  selector: 'app-stat-list',
  templateUrl: './stat-list.component.html',
  styleUrls: ['./stat-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
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
  @Input() data$!: Observable<PieDataItem[]>;
  /**
   * Optional callback handler when an item is clicked
   */
  @Input() handleClick: ((data: PieDataItem) => void) | undefined = undefined;

  doClick(item: PieDataItem) {
    if (!this.handleClick) return; 
    this.handleClick(item);
  }

}
