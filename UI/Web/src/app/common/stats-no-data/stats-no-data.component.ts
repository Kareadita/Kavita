import {ChangeDetectionStrategy, Component, input} from '@angular/core';

@Component({
  selector: 'app-stats-no-data',
  imports: [],
  templateUrl: './stats-no-data.component.html',
  styleUrl: './stats-no-data.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatsNoDataComponent {

  /**
   * No Data message
   */
  message = input.required<string>();
  /**
   * Icon - Must include fa-solid/etc
   */
  icon = input<string>('fa-solid fa-users-slash');

}
