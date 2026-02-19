import {
  ChangeDetectionStrategy,
  Component,
  contentChild,
  input,
  model,
  TemplateRef
} from '@angular/core';
import {NgTemplateOutlet} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
    selector: 'app-setting-title',
    imports: [
        NgTemplateOutlet,
        TranslocoDirective
    ],
    templateUrl: './setting-title.component.html',
    styleUrl: './setting-title.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SettingTitleComponent {

  title = input.required<string>();
  /**
   * If passed, will generate a proper label element. Requires `id` to be passed as well
   */
  labelId = input<string | undefined>();
  id = input<string | undefined>();
  canEdit = input(true);
  isEditMode = model(false);
  titleExtraRef = contentChild<TemplateRef<any>>('extra');

  toggleViewMode() {
    this.isEditMode.set(!this.isEditMode());
  }

}
