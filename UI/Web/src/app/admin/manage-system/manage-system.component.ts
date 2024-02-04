import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {ServerService} from 'src/app/_services/server.service';
import {ServerInfoSlim} from '../_models/server-info';
import {NgIf} from '@angular/common';
import {TranslocoDirective} from "@ngneat/transloco";
import {ChangelogComponent} from "../../announcements/_components/changelog/changelog.component";

@Component({
    selector: 'app-manage-system',
    templateUrl: './manage-system.component.html',
    styleUrls: ['./manage-system.component.scss'],
    standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgIf, TranslocoDirective, ChangelogComponent]
})
export class ManageSystemComponent implements OnInit {

  serverInfo!: ServerInfoSlim;
  private readonly cdRef = inject(ChangeDetectorRef);


  constructor(public serverService: ServerService) { }

  ngOnInit(): void {

    this.serverService.getServerInfo().subscribe(info => {
      this.serverInfo = info;
      this.cdRef.markForCheck();
    });
  }
}
