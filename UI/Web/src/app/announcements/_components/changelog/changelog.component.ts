import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {UpdateVersionEvent} from 'src/app/_models/events/update-version-event';
import {ServerService} from 'src/app/_services/server.service';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";

import {
  NgbAccordionBody,
  NgbAccordionButton, NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem
} from "@ng-bootstrap/ng-bootstrap";
import {ChangelogUpdateItemComponent} from "../changelog-update-item/changelog-update-item.component";

@Component({
  selector: 'app-changelog',
  templateUrl: './changelog.component.html',
  styleUrls: ['./changelog.component.scss'],
  standalone: true,
  imports: [LoadingComponent, TranslocoDirective, NgbAccordionDirective,
    NgbAccordionItem, NgbAccordionButton, NgbAccordionHeader, NgbAccordionCollapse, NgbAccordionBody, ChangelogUpdateItemComponent],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangelogComponent implements OnInit {

  private readonly serverService = inject(ServerService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly accountService = inject(AccountService);

  updates: Array<UpdateVersionEvent> = [];
  isLoading: boolean = true;

  ngOnInit(): void {
    this.serverService.getChangelog(30).subscribe(updates => {
      this.updates = updates;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }
}
