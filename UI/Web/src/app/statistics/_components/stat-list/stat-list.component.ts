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

  @Input() title: string = ''
  @Input() description: string = '';
  @Input() data$!: Observable<PieDataItem[]>;

}
