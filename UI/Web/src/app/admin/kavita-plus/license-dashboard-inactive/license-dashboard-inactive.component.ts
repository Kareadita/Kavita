import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {TranslocoDirective} from '@jsverse/transloco';
import {WikiLink} from '../../../_models/wiki';
import {LicenseInfo} from '../../../_models/kavitaplus/license-info';
import {LicenseInfoPanelComponent} from '../license-info-panel/license-info-panel.component';

@Component({
  selector: 'app-license-dashboard-inactive',
  imports: [
    TranslocoDirective,
    LicenseInfoPanelComponent,
  ],
  templateUrl: './license-dashboard-inactive.component.html',
  styleUrl: './license-dashboard-inactive.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LicenseDashboardInactiveComponent {

  licenseInfo = input.required<LicenseInfo | null>();

  protected readonly WikiLink = WikiLink;
}
