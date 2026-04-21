import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {SafeUrlPipe} from "../../_pipes/safe-url.pipe";

export type LabelCardValueColor = 'default' | 'green' | 'muted';

@Component({
  selector: 'app-label-card',
  templateUrl: './label-card.component.html',
  styleUrl: './label-card.component.scss',
  imports: [
    SafeUrlPipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LabelCardComponent {
  label = input.required<string>();
  value = input<string | number | null | undefined>();
  /** When link provided, the value will render as a link **/
  linkUrl = input<string | undefined>(undefined);
}
