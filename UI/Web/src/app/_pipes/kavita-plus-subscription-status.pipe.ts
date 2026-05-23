import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {KavitaPlusSubscriptionState} from '../_models/kavitaplus/license-info';

@Pipe({
  name: 'kavitaPlusSubscriptionStatus',
  standalone: true,
  pure: true
})
export class KavitaPlusSubscriptionStatusPipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(state: KavitaPlusSubscriptionState | null | undefined): string {
    switch (state) {
      case KavitaPlusSubscriptionState.Active:    return this.translocoService.translate('kavita-plus-subscription-status-pipe.active-label');
      case KavitaPlusSubscriptionState.Cancelled: return this.translocoService.translate('kavita-plus-subscription-status-pipe.cancelled-label');
      case KavitaPlusSubscriptionState.Paused:    return this.translocoService.translate('kavita-plus-subscription-status-pipe.paused-label');
      default:                                    return '';
    }
  }
}
