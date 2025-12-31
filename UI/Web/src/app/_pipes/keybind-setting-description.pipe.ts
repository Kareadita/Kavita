import {Pipe, PipeTransform} from '@angular/core';
import {KeyBindTarget} from "../_models/preferences/preferences";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'keybindSettingDescription'
})
export class KeybindSettingDescriptionPipe implements PipeTransform {

  prefix = 'keybind-setting-description-pipe';
  transform(value: KeyBindTarget) {
    switch (value) {
      case KeyBindTarget.NavigateToSettings:
        return this.create('key-bind-title-navigate-to-settings', 'key-bind-tooltip-navigate-to-settings');
      case KeyBindTarget.OpenSearch:
        return this.create('key-bind-title-open-search', 'key-bind-tooltip-open-search');
      case KeyBindTarget.NavigateToScrobbling:
        return this.create('key-bind-title-navigate-to-scrobbling', 'key-bind-tooltip-navigate-to-scrobbling');
      case KeyBindTarget.ToggleFullScreen:
        return this.create('key-bind-title-toggle-fullscreen', 'key-bind-tooltip-toggle-fullscreen');
      case KeyBindTarget.BookmarkPage:
        return this.create('key-bind-title-bookmark-page', 'key-bind-tooltip-bookmark-page');
      case KeyBindTarget.OpenHelp:
        return this.create('key-bind-title-open-help', 'key-bind-tooltip-open-help');
      case KeyBindTarget.GoTo:
        return this.create('key-bind-title-go-to', 'key-bind-tooltip-go-to');
      case KeyBindTarget.ToggleMenu:
        return this.create('key-bind-title-toggle-menu', 'key-bind-tooltip-toggle-menu');
      case KeyBindTarget.PageLeft:
        return this.create('key-bind-title-page-left', 'key-bind-tooltip-page-left');
      case KeyBindTarget.PageRight:
        return this.create('key-bind-title-page-right', 'key-bind-tooltip-page-right');
      case KeyBindTarget.Escape:
        return this.create('key-bind-title-escape', 'key-bind-tooltip-escape');
      case KeyBindTarget.PageUp:
        return this.create('key-bind-title-page-up', 'key-bind-tooltip-page-up');
      case KeyBindTarget.PageDown:
        return this.create('key-bind-title-page-down', 'key-bind-tooltip-page-down');
      case KeyBindTarget.OffsetDoublePage:
        return this.create('key-bind-title-offset-double-page', 'key-bind-tooltip-offset-double-page');
    }
  }

  private create(titleKey: string, tooltipKey: string) {

    return {title: translate(`${this.prefix}.${titleKey}`), tooltip: translate(`${this.prefix}.${tooltipKey}`)}
}

}
