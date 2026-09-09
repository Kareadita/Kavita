import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  model
} from '@angular/core';
import {
  FormsModule,
  ReactiveFormsModule
} from "@angular/forms";
import {DefaultValuePipe} from "../../../_pipes/default-value.pipe";
import {SettingItemComponent} from "../setting-item/setting-item.component";
import {TagBadgeComponent} from "../../../shared/tag-badge/tag-badge.component";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {FormField, FormValueControl} from "@angular/forms/signals";

/**
 * SettingMultiTextFieldComponent should be used when using a text area to input several comma separated values.
 * The component should have a formField for string[]
 *
 * An example can be found in ManageOpenIDConnectComponent
 */
@Component({
  selector: 'app-setting-multi-text-field',
  imports: [
    DefaultValuePipe,
    FormsModule,
    ReactiveFormsModule,
    SettingItemComponent,
    TagBadgeComponent,
    FormFieldDirective
  ],
  templateUrl: './setting-multi-text-field.component.html',
  styleUrl: './setting-multi-text-field.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingMultiTextFieldComponent implements FormValueControl<string[]> {

  protected fieldDirective = inject(FormField);

  /**
   * Filter, required if your type is not a string
   * @default non empty strings
   */
  valueFilter = input<(t: string) => boolean>(t => t.length > 0);
  /**
   * Title to display
   */
  title = input.required<string>();
  /**
   * Tooltip to display
   * @optional
   */
  tooltip = input<string>('');
  /**
   * Loading indicator for the checkbox list
   * @optional
   */
  loading = input<boolean | undefined>(undefined);

  isLoading = computed(() => {
    const loading = this.loading();
    return loading !== undefined && loading;
  });
  textFieldValue = computed(() => this.value().join(','))

  value = model<string[]>([]);
  disabled = input(false);

  onTextFieldChange(event: Event) {
    const input = (event.target as HTMLTextAreaElement).value;
    this.value.set(input.split(',').filter(this.valueFilter())
    );
  }
}
