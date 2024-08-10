import { Pipe, PipeTransform } from '@angular/core';
import {SettingsTabId} from "../sidenav/preference-nav/preference-nav.component";
import {translate} from "@jsverse/transloco";

/**
 * Translates the fragment for Settings to a User title
 */
@Pipe({
  name: 'settingFragment',
  standalone: true
})
export class SettingFragmentPipe implements PipeTransform {

  transform(tabID: SettingsTabId | string): string {
    return translate('settings.' + tabID);
  }
}
