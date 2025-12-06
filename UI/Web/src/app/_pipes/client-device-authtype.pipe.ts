import {Pipe, PipeTransform} from '@angular/core';
import {AuthenticationType} from "../_models/progress/reading-session";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'clientDeviceAuthType'
})
export class ClientDeviceAuthTypePipe implements PipeTransform {

  transform(value: AuthenticationType) {
    switch (value) {
      case AuthenticationType.Unknown:
        return translate('client-device-auth-type-pipe.unknown');
      case AuthenticationType.JWT:
        return translate('client-device-auth-type-pipe.jwt');
      case AuthenticationType.AuthKey:
        return translate('client-device-auth-type-pipe.auth-key');
      case AuthenticationType.OIDC:
        return translate('client-device-auth-type-pipe.oidc');
    }
  }

}
