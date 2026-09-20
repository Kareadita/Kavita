import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  model
} from '@angular/core';
import {RgbaColor} from "../../../book-reader/_models/annotations/highlight-slot";
import {FormValueControl} from "@angular/forms/signals";
import {TranslocoDirective} from "@jsverse/transloco";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {NgStyle} from "@angular/common";

/**
 * An item to display in the SettingMultiCheckBox
 */
export interface MultiCheckBoxItem<T> {
  /**
   * Label to display in the list
   */
  label: string,
  /**
   * Value passed to the field
   */
  value: T,
  /**
   * Appends a dot after the label
   */
  colour?: RgbaColor,
  /**
   * If the items checkbox should be disabled. Does not overwrite global disable
   * @param value
   * @param selected
   */
  disableFunc?: (value: T, selected: T[]) => boolean,
}

/**
 * The SettingMultiCheckBox should be used when wanting to display all options, of which any may be selected at once.
 * The component should have a formField bound to it of type FieldTree<T[]>.
 *
 * An example can be found in ManageUserPreferencesComponent
 */
@Component({
  selector: 'app-setting-multi-check-box',
  imports: [
    TranslocoDirective,
    LoadingComponent,
    NgStyle
  ],
  standalone: true,
  templateUrl: './setting-multi-check-box.component.html',
  styleUrl: './setting-multi-check-box.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingMultiCheckBox<T> implements FormValueControl<T[]> {

  /**
   * Id to prepend to input id to ensure uniqueness
   */
  id = input.required<string>();
  /**
   * Title to display above the checkboxes
   */
  title = input.required<string>();
  /**
   * Tooltip to display muted underneath the title
   * @optional
   */
  tooltip = input<string>('');
  /**
   * Loading indicator for the checkbox list
   * @optional
   */
  loading = input<boolean | undefined>(undefined);
  /**
   * All possible options
   */
  options = input.required<MultiCheckBoxItem<T>[]>();
  /**
   * Disable all checkboxes
   */
  disabled = input(false);
  /**
   * An optional warning to display underneath the title
   * @optional
   */
  warning = input<string | undefined>(undefined);

  isLoading = computed(() => {
    const loading = this.loading();
    return loading !== undefined && loading;
  });
  allSelected = computed(() => this.options().length === this.value().length);

  value = model<T[]>([]);

  isChecked(item: MultiCheckBoxItem<T>) {
    return this.value().includes(item.value);
  }

  isDisabled(item: MultiCheckBoxItem<T>) {
    const disabled = this.disabled();
    const selected = this.value();

    if (disabled) {
      return true;
    }

    return item.disableFunc && item.disableFunc(item.value, selected);
  }

  onCheckboxChange(item: MultiCheckBoxItem<T>, event: Event) {
    const checked = (event.target as HTMLInputElement).checked;

    if (checked) {
      this.value.update(x => [...x, item.value]);
    } else {
      this.value.update(x => x.filter(t => t !== item.value));
    }
  }

  toggleAll() {
    if (this.allSelected()) {
      this.value.set([]);
    } else {
      this.value.set(this.options().map(opt => opt.value));
    }
  }

}
