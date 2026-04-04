import { Pipe, PipeTransform } from '@angular/core';
import {AuthorityValidationResult} from "../_models/oidc-config";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'authorityValidationResult',
})
export class AuthorityValidationResultPipe implements PipeTransform {

  transform(value: AuthorityValidationResult): string {
    switch (value) {
      case AuthorityValidationResult.Success:
        return translate('authority-validation-result-pipe.success');
      case AuthorityValidationResult.InvalidAuthority:
        return translate('authority-validation-result-pipe.invalid-authority');
      case AuthorityValidationResult.Failure:
        return translate('authority-validation-result-pipe.failure');
      case AuthorityValidationResult.NotApplicable:
        return translate('authority-validation-result-pipe.not-applicable');
      case AuthorityValidationResult.MissingHttps:
        return translate('authority-validation-result-pipe.missing-https');
    }
  }

}
