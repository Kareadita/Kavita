import { Pipe, PipeTransform } from '@angular/core';
import {KeyBind} from "../_models/preferences/preferences";
import {KeyCode} from "../_services/key-bind.service";

@Pipe({
  name: 'keyBind'
})
export class KeyBindPipe implements PipeTransform {

  private readonly customMappings: Partial<Record<KeyCode, string>> = {
    [KeyCode.ArrowDown]: '↓',
    [KeyCode.ArrowUp]: '↑',
    [KeyCode.ArrowLeft]: '⇽',
    [KeyCode.ArrowRight]: '⇾',
    [KeyCode.Space]: 'space',
  } as const;

  transform(keyBind: KeyBind | undefined): string {
    if (!keyBind) return '';

    if (keyBind.controllerSequence) {
      return keyBind.controllerSequence.join('+');
    }

    let keys: string[] = [];

    if (keyBind.control) keys.push('Ctrl');
    if (keyBind.shift) keys.push('Shift');
    if (keyBind.alt) keys.push('Alt');

    // TODO: Use new device code after progress merge?
    const isMac = navigator.platform.includes('Mac');
    if (keyBind.meta) keys.push(isMac ? '⌘' : 'Win');

    keys.push(this.customMappings[keyBind.key] ?? keyBind.key.toUpperCase())

    return keys.join('+')
  }

}
