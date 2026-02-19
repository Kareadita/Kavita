import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {TranslocoDirective} from "@jsverse/transloco";

/**
 * Use with btn-sm
 */
@Component({
  selector: 'app-setting-button',
  imports: [
      SafeHtmlPipe,
      TranslocoDirective
  ],
  templateUrl: './setting-button.component.html',
  styleUrl: './setting-button.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingButtonComponent {

  subtitle = input.required<string>();

}
