import {ChangeDetectionStrategy, Component, inject, Input} from '@angular/core';
import {NgbActiveModal, NgbModalModule} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from "@jsverse/transloco";
import {KeyBindTarget} from "../../../_models/preferences/preferences";
import {KeyBindService} from "../../../_services/key-bind.service";
import {KeyBindPipe} from "../../../_pipes/key-bind.pipe";
import {KeybindSettingDescriptionPipe} from "../../../_pipes/keybind-setting-description.pipe";

export interface KeyboardShortcut {
  /**
   * String representing key or key combo. Should use + for combos. Will render as upper case
   */
  key?: string;
  /**
   * Description of how it works
   */
  description?: string;
  /**
   * Keybind target, will display the first configured keybind instead of the given key
   */
  keyBindTarget?: KeyBindTarget;
}

@Component({
  selector: 'app-shortcuts-modal',
  imports: [NgbModalModule, TranslocoDirective, KeyBindPipe, KeybindSettingDescriptionPipe],
  templateUrl: './shortcuts-modal.component.html',
  styleUrls: ['./shortcuts-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ShortcutsModalComponent {

  protected readonly keyBindService = inject(KeyBindService);
  protected readonly modal = inject(NgbActiveModal);

  @Input() shortcuts: Array<KeyboardShortcut> = [];
}
