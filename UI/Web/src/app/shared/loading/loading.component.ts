import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

import {TranslocoDirective} from "@jsverse/transloco";

/**
 * Simple loading circle, displays content if loading is false for easy wrapping
 */
@Component({
    selector: 'app-loading',
    imports: [TranslocoDirective],
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
