import { Pipe, PipeTransform } from '@angular/core';
import {KeyBind} from "../_models/preferences/preferences";

@Pipe({
  name: 'keyBind'
})
export class KeyBindPipe implements PipeTransform {

  transform(keyBind?: KeyBind): string {
    if (!keyBind) return '';

    let keys: string[] = [];

    if (keyBind.control) keys.push('Ctrl');
    if (keyBind.shift) keys.push('Shift');
    if (keyBind.alt) keys.push('Alt');

    // TODO: Use new device code after progress merge?
    const isMac = navigator.platform.includes('Mac');
    if (keyBind.meta) keys.push(isMac ? '⌘' : 'Win');

    keys.push(keyBind.key.toUpperCase())

    return keys.join("+")
  }

}
