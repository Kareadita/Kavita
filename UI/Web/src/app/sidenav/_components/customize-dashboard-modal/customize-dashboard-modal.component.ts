import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {CommonModule} from '@angular/common';
import {SafeHtmlPipe} from "../../../pipe/safe-html.pipe";
import {TranslocoDirective} from "@ngneat/transloco";
import {NgbActiveModal, NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from "@ng-bootstrap/ng-bootstrap";
import {
  DraggableOrderedListComponent,
  IndexUpdateEvent
} from "../../../reading-list/_components/draggable-ordered-list/draggable-ordered-list.component";
import {
  ReadingListItemComponent
} from "../../../reading-list/_components/reading-list-item/reading-list-item.component";
import {forkJoin} from "rxjs";
import {FilterService} from "../../../_services/filter.service";
import {DashboardStreamListItemComponent} from "../dashboard-stream-list-item/dashboard-stream-list-item.component";
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {DashboardService} from "../../../_services/dashboard.service";
import {DashboardStream} from "../../../_models/dashboard/dashboard-stream";
import {Breakpoint, UtilityService} from "../../../shared/_services/utility.service";
import {CustomizeDashboardStreamsComponent} from "../customize-dashboard-streams/customize-dashboard-streams.component";
import {CustomizeSidenavStreamsComponent} from "../customize-sidenav-streams/customize-sidenav-streams.component";
import {ManageExternalSourcesComponent} from "../manage-external-sources/manage-external-sources.component";

enum TabID {
  Dashboard = 'dashboard',
  SideNav = 'sidenav',
  ExternalSources = 'external-sources'
}

@Component({
  selector: 'app-customize-dashboard-modal',
  standalone: true,
  imports: [CommonModule, SafeHtmlPipe, TranslocoDirective, DraggableOrderedListComponent, ReadingListItemComponent, DashboardStreamListItemComponent,
    NgbNav, NgbNavContent, NgbNavLink, NgbNavItem, NgbNavOutlet, CustomizeDashboardStreamsComponent, CustomizeSidenavStreamsComponent, ManageExternalSourcesComponent],
  templateUrl: './customize-dashboard-modal.component.html',
  styleUrls: ['./customize-dashboard-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CustomizeDashboardModalComponent {

  activeTab = TabID.Dashboard;

  private readonly cdRef = inject(ChangeDetectorRef);
  public readonly utilityService = inject(UtilityService);
  private readonly modal = inject(NgbActiveModal);
  protected readonly TabID = TabID;
  protected readonly Breakpoint = Breakpoint;

  close() {
    this.modal.close();
  }
}
