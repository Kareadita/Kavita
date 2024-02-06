import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {UpdateVersionEvent} from 'src/app/_models/events/update-version-event';
import {ServerService} from 'src/app/_services/server.service';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {ReadMoreComponent} from '../../../shared/read-more/read-more.component';
import {DatePipe, NgFor, NgIf} from '@angular/common';
import {TranslocoDirective} from "@ngneat/transloco";

@Component({
  selector: 'app-changelog',
  templateUrl: './changelog.component.html',
  styleUrls: ['./changelog.component.scss'],
  standalone: true,
  imports: [NgFor, NgIf, ReadMoreComponent, LoadingComponent, DatePipe, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangelogComponent implements OnInit {

  private readonly serverService = inject(ServerService);
  private readonly cdRef = inject(ChangeDetectorRef);
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
