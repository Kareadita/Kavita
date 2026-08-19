import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {ServerService} from 'src/app/_services/server.service';
import {ServerInfoSlim} from '../_models/server-info';
import {TranslocoDirective} from "@jsverse/transloco";
import {ChangelogComponent} from "../../announcements/_components/changelog/changelog.component";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";

@Component({
    selector: 'app-manage-system',
    templateUrl: './manage-system.component.html',
    styleUrls: ['./manage-system.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, ChangelogComponent, UtcToLocalTimePipe]
})
export class ManageSystemComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly serverService = inject(ServerService);

  serverInfo!: ServerInfoSlim;

  ngOnInit(): void {
    this.serverService.getServerInfo().subscribe(info => {
      this.serverInfo = info;
      this.cdRef.markForCheck();
    });
  }
}
