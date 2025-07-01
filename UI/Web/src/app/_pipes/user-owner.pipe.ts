import { Pipe, PipeTransform } from '@angular/core';
import {UserOwner} from "../_models/user";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'userOwnerPipe'
})
export class UserOwnerPipe implements PipeTransform {

  transform(value: UserOwner, ...args: unknown[]): string {
    switch (value) {
      case UserOwner.Native:
        return translate("creation-source-pipe.native");
      case UserOwner.OpenIdConnect:
        return translate("creation-source-pipe.oidc");
    }
  }

}
