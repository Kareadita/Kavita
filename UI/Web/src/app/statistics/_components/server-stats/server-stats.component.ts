import {ChangeDetectionStrategy, Component, computed, inject, signal} from '@angular/core';
import {NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";
import {ReactiveFormsModule} from "@angular/forms";
import {StatsFilter} from "../../_models/stats-filter";
import {ServerStatsStatsTabComponent} from "../server-stats-stats-tab/server-stats-stats-tab.component";
import {ServerStatsMgmtTabComponent} from "../server-stats-mgmt-tab/server-stats-mgmt-tab.component";

enum TabID {
  Stats = 'stats-tab',
  Management = 'management-tab',
}

@Component({
    selector: 'app-server-stats',
    templateUrl: './server-stats.component.html',
    styleUrls: ['./server-stats.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, ReactiveFormsModule, NgbNav, NgbNavContent, NgbNavLink, NgbNavItem, NgbNavOutlet, ServerStatsStatsTabComponent, ServerStatsMgmtTabComponent]
})
export class ServerStatsComponent {
  protected readonly accountService = inject(AccountService);


  activeTabId = TabID.Stats;

  userId = computed(() => this.accountService.currentUserSignal()?.id);
  readonly filter = signal<StatsFilter | undefined>(undefined);
  readonly year = signal<number>(new Date().getFullYear());

  protected readonly TabID = TabID;
}
