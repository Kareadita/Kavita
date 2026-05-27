import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-empty-state',
  imports: [TranslocoDirective],
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmptyStateComponent {
  isError = input<boolean>(false);
  titleKey = input.required<string>();
  descriptionKey = input<string>('');
  i18nPrefix = input<string>('');
  descriptionParams = input<Record<string, unknown>>({});
}
