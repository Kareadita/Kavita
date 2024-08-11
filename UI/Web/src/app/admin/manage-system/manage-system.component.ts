import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {ServerService} from 'src/app/_services/server.service';
import {ServerInfoSlim} from '../_models/server-info';
import {DatePipe, NgIf} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {ChangelogComponent} from "../../announcements/_components/changelog/changelog.component";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";

@Component({
    selector: 'app-manage-system',
    templateUrl: './manage-system.component.html',
    styleUrls: ['./manage-system.component.scss'],
    standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgIf, TranslocoDirective, ChangelogComponent, DefaultDatePipe, DefaultValuePipe, DatePipe]
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
