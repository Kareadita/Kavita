import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import {CommonModule} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-loading',
  standalone: true,
  imports: [CommonModule, TranslocoDirective],
  templateUrl: './loading.component.html',
  styleUrls: ['./loading.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoadingComponent {

  @Input() loading: boolean = false;
  @Input() message: string = '';
  @Input() size: '' | 'spinner-border-sm' = '';
  /**
   * Uses absolute positioning to ensure it loads over content
   */
  @Input() absolute: boolean = false;
}
