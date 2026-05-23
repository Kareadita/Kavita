import {ChangeDetectionStrategy, Component, input} from '@angular/core';
import {TranslocoDirective} from '@jsverse/transloco';
import {WikiLink} from '../../../_models/wiki';
import {LicenseInfo} from '../../../_models/kavitaplus/license-info';
import {LicenseInfoPanelComponent} from '../license-info-panel/license-info-panel.component';

@Component({
  selector: 'app-license-dashboard',
  imports: [
    TranslocoDirective,
    LicenseInfoPanelComponent,
  ],
  templateUrl: './license-dashboard.component.html',
  styleUrl: './license-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LicenseDashboardComponent {

  licenseInfo = input.required<LicenseInfo | null>();

  protected readonly WikiLink = WikiLink;
}
