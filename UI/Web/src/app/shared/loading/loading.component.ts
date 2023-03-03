import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-loading',
  templateUrl: './loading.component.html',
  styleUrls: ['./loading.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoadingComponent {

  @Input() loading: boolean = false;
  @Input() message: string = '';
  /**
   * Uses absolute positioning to ensure it loads over content
   */
  @Input() absolute: boolean = false;
  
  constructor() { }
}
