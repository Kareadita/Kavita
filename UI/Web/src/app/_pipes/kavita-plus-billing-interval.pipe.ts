import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {KavitaPlusBillingInterval} from '../_models/kavitaplus/license-info';

@Pipe({
  name: 'kavitaPlusBillingInterval',
  standalone: true,
  pure: true
})
export class KavitaPlusBillingIntervalPipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(interval: KavitaPlusBillingInterval | null | undefined, mode: 'adjective' | 'unit' = 'adjective'): string {
    const suffix = mode === 'unit' ? '-unit-label' : '-label';
    switch (interval) {
      case KavitaPlusBillingInterval.Day:  return this.translocoService.translate('kavita-plus-billing-interval-pipe.day' + suffix);
      case KavitaPlusBillingInterval.Week: return this.translocoService.translate('kavita-plus-billing-interval-pipe.week' + suffix);
      case KavitaPlusBillingInterval.Year: return this.translocoService.translate('kavita-plus-billing-interval-pipe.year' + suffix);
      default:                             return this.translocoService.translate('kavita-plus-billing-interval-pipe.month' + suffix);
    }
  }
}
