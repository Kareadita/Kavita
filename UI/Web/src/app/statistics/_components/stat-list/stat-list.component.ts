import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
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

}
