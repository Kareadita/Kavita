import {Pipe, PipeTransform} from '@angular/core';
import {Role} from "../_services/account.service";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'roleLocalized'
})
export class RoleLocalizedPipe implements PipeTransform {

  transform(value: Role | string): string {
    const key = (value + '').toLowerCase().replace(' ', '-');
    return translate(`role-localized-pipe.${key}`);
  }

}
