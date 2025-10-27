import { Pipe, PipeTransform } from '@angular/core';
import {KeyBind} from "../_models/preferences/preferences";

@Pipe({
  name: 'keyBind'
})
export class KeyBindPipe implements PipeTransform {

  transform(keyBind: KeyBind): string {
    let keys: string[] = [];

    if (keyBind.control) keys.push('Control');
    if (keyBind.shift) keys.push('Shift');
    if (keyBind.alt) keys.push('Alt');
    if (keyBind.meta) keys.push('⌘');

    keys.push(keyBind.key)

    return keys.join("+")
  }

}
