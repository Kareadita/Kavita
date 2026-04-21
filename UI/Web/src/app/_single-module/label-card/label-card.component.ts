import {ChangeDetectionStrategy, Component, input} from '@angular/core';

export type LabelCardValueColor = 'default' | 'green' | 'muted';

@Component({
  selector: 'app-label-card',
  templateUrl: './label-card.component.html',
  styleUrl: './label-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LabelCardComponent {
  icon = input<string>();
  label = input.required<string>();
  value = input<string | number | null | undefined>();
  valueColor = input<LabelCardValueColor>('default');
}
