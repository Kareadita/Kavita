import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {KavitaPlusProviderHealthStatus} from '../_models/kavitaplus/kavita-plus-provider-health';

@Pipe({
  name: 'kavitaPlusProviderHealthStatus',
  standalone: true,
  pure: true,
})
export class KavitaPlusProviderHealthStatusPipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(status: KavitaPlusProviderHealthStatus | null | undefined): string {
    switch (status) {
      case KavitaPlusProviderHealthStatus.Operational: return this.translocoService.translate('kavita-plus-provider-health-status-pipe.operational-label');
      case KavitaPlusProviderHealthStatus.Degraded:    return this.translocoService.translate('kavita-plus-provider-health-status-pipe.degraded-label');
      case KavitaPlusProviderHealthStatus.Down:        return this.translocoService.translate('kavita-plus-provider-health-status-pipe.down-label');
      default:                                         return this.translocoService.translate('kavita-plus-provider-health-status-pipe.unknown-label');
    }
  }
}
