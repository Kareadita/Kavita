import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {UpdateVersionEvent} from 'src/app/_models/events/update-version-event';
import {ServerService} from 'src/app/_services/server.service';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {ReadMoreComponent} from '../../../shared/read-more/read-more.component';
import {AsyncPipe, DatePipe} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";

@Component({
  selector: 'app-changelog',
  templateUrl: './changelog.component.html',
  styleUrls: ['./changelog.component.scss'],
  standalone: true,
  imports: [ReadMoreComponent, LoadingComponent, DatePipe, TranslocoDirective, AsyncPipe],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangelogComponent implements OnInit {

  private readonly serverService = inject(ServerService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly accountService = inject(AccountService);

  updates: Array<UpdateVersionEvent> = [];
  isLoading: boolean = true;

  ngOnInit(): void {
    this.serverService.getChangelog().subscribe(updates => {
      this.updates = updates;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }
}
