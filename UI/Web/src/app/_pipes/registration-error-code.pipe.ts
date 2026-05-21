import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {KavitaPlusRegistrationErrorCode} from '../_models/kavitaplus/registration/kavita-plus-registration-error-code';

@Pipe({
  name: 'kavitaPlusRegistrationErrorCode',
  standalone: true,
  pure: true
})
export class KavitaPlusRegistrationErrorCodePipe implements PipeTransform {

  private readonly translocoService = inject(TranslocoService);

  transform(code: KavitaPlusRegistrationErrorCode | null | undefined): string {
    if (code == null) return '';

    switch (code) {
      case KavitaPlusRegistrationErrorCode.RegistrationFailed:
        return this.translocoService.translate('kavita-plus-registration-error-code-pipe.registration-failed');
      case KavitaPlusRegistrationErrorCode.AlreadyRegistered:
        return this.translocoService.translate('kavita-plus-registration-error-code-pipe.already-registered');
      case KavitaPlusRegistrationErrorCode.SubscriptionInactive:
        return this.translocoService.translate('kavita-plus-registration-error-code-pipe.subscription-inactive');
      case KavitaPlusRegistrationErrorCode.InternalError:
        return this.translocoService.translate('kavita-plus-registration-error-code-pipe.internal-error');
      default:
        return this.translocoService.translate('kavita-plus-registration-error-code-pipe.internal-error');
    }
  }
}
