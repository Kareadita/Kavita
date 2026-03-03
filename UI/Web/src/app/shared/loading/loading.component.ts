import {ChangeDetectionStrategy, Component, input} from '@angular/core';

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

  loading = input<boolean>(false);
  message = input<string>('');
  size = input<'' | 'spinner-border-sm'>('');
  /**
   * Uses absolute positioning to ensure it loads over content
   */
  absolute = input<boolean>(false);

}
