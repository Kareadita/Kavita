import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {Select2, Select2Data} from "ng-select2-component";
import {AccordionComponent} from "../../shared/accordion/accordion.component";
import {TagBadgeComponent, TagBadgeCursor} from "../../shared/tag-badge/tag-badge.component";
import {ConfidenceChipComponent} from "../../shared/_components/confidence-chip/confidence-chip.component";
import {MediaFormatPillComponent} from "../../shared/_components/media-format-pill/media-format-pill.component";
import {
  ScrobbleProviderTagBadgeComponent
} from "../../shared/_components/scrobble-provider-tag-badge/scrobble-provider-tag-badge.component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {
  SettingMultiTextFieldComponent
} from "../../settings/_components/setting-multi-text-field/setting-multi-text-field.component";
import {
  MultiCheckBoxItem,
  SettingMultiCheckBox
} from "../../settings/_components/setting-multi-check-box/setting-multi-check-box.component";
import {PlusMediaFormat} from "../../_models/series-detail/external-series-detail";
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {ConfirmService} from "../../shared/confirm.service";
import {ConfirmConfig} from "../../shared/confirm-dialog/_models/confirm-config";

/**
 * Developer-only, unlocalized style guide. Renders the app's common UI primitives and raw theme
 * tokens in one place so visual changes to the SCSS themes (dark.scss + theme/components/*) can be
 * evaluated instantly. Reachable at /theme.
 */
@Component({
  selector: 'app-theme',
  imports: [
    AccordionComponent,
    Select2,
    TagBadgeComponent,
    ConfidenceChipComponent,
    MediaFormatPillComponent,
    ScrobbleProviderTagBadgeComponent,
    SettingItemComponent,
    SettingSwitchComponent,
    SettingMultiTextFieldComponent,
    SettingMultiCheckBox,
  ],
  templateUrl: './theme.component.html',
  styleUrl: './theme.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThemeComponent {

  protected readonly confirmService = inject(ConfirmService);

  protected readonly TagBadgeCursor = TagBadgeCursor;
  protected readonly PlusMediaFormat = PlusMediaFormat;
  protected readonly ScrobbleProvider = ScrobbleProvider;

  protected readonly select2Demo: Select2Data = [
    {value: 1, label: 'Option A'},
    {value: 2, label: 'Option B'},
    {value: 3, label: 'Option C'},
  ];

  protected readonly settingCheckboxOptions: MultiCheckBoxItem<number>[] = [
    {label: 'Option One', value: 1},
    {label: 'Option Two', value: 2},
    {label: 'Option Three', value: 3},
  ];

  confirm() {
    this.confirmService.confirm('Test content');
  }

  alert() {
    this.confirmService.alert('Test content');
  }

  info() {
    this.confirmService.info('Test content', 'Info');
  }

  prompt() {
    this.confirmService.prompt('Enter a value');
  }

  dangerConfirm() {
    const config = new ConfirmConfig();
    config.header = 'Delete series';
    config.content = 'This action cannot be undone.';
    config.buttons = [
      {text: 'Cancel', type: 'secondary'},
      {text: 'Delete', type: 'danger'},
    ];
    this.confirmService.confirm(undefined, config);
  }

}
