import {ChangeDetectionStrategy, Component} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {DayBreakdownComponent} from "../day-breakdown/day-breakdown.component";
import {FileBreakdownStatsComponent} from "../file-breakdown-stats/file-breakdown-stats.component";
import {PublicationStatusStatsComponent} from "../publication-status-stats/publication-status-stats.component";
import {FilesOverTimeComponent} from "../files-over-time/files-over-time.component";

@Component({
  selector: 'app-server-stats-mgmt-tab',
  imports: [
    TranslocoDirective,
    DayBreakdownComponent,
    FileBreakdownStatsComponent,
    PublicationStatusStatsComponent,
    FilesOverTimeComponent
  ],
  templateUrl: './server-stats-mgmt-tab.component.html',
  styleUrl: './server-stats-mgmt-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServerStatsMgmtTabComponent {

}
