import {ChangeDetectionStrategy, Component, computed, input, model, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {generateUniqueId} from "../../../_helpers/random";

@Component({
  selector: 'app-filter-field',
  imports: [TranslocoDirective],
  styleUrl: './filter-field.component.scss',
  templateUrl: './filter-field.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FilterFieldComponent {

  query = model<string>('');
  /** Visible/screen-reader label and placeholder. Defaults to common.filter */
  label = input<string>();
  showLabel = input<boolean>(false);
  clearMode = input<'icon' | 'button'>('icon');
  disabled = input<boolean>(false);
  size = input<'sm' | undefined>(undefined);

  protected readonly generatedId = signal<string>(generateUniqueId());
  protected readonly clearId = computed(() => this.generatedId() + '-clear');

  protected updateQuery(event: Event) {
    this.query.set((event.target as HTMLInputElement).value);
  }

  protected clearField() {
    this.query.set('');
  }

}
