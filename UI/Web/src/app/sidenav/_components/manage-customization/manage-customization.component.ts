import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {Breakpoint, UtilityService} from "../../../shared/_services/utility.service";
import {CustomizeDashboardStreamsComponent} from "../customize-dashboard-streams/customize-dashboard-streams.component";
import {CustomizeSidenavStreamsComponent} from "../customize-sidenav-streams/customize-sidenav-streams.component";
import {ManageExternalSourcesComponent} from "../manage-external-sources/manage-external-sources.component";
import {ManageSmartFiltersComponent} from "../manage-smart-filters/manage-smart-filters.component";
import {NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from 'src/app/_models/wiki';

enum TabID {
  Dashboard = 'dashboard',
  SideNav = 'sidenav',
  SmartFilters = 'smart-filters',
  ExternalSources = 'external-sources'
}

@Component({
  selector: 'app-manage-customization',
  standalone: true,
  imports: [
    CustomizeDashboardStreamsComponent,
    CustomizeSidenavStreamsComponent,
    ManageExternalSourcesComponent,
    ManageSmartFiltersComponent,
    NgbNav,
    NgbNavContent,
    NgbNavLink,
    TranslocoDirective,
    NgbNavItem,
    NgbNavOutlet
  ],
  templateUrl: './manage-customization.component.html',
  styleUrl: './manage-customization.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageCustomizationComponent {
  private readonly cdRef = inject(ChangeDetectorRef);
  public readonly utilityService = inject(UtilityService);

  protected readonly TabID = TabID;
  protected readonly Breakpoint = Breakpoint;
  protected readonly WikiLink = WikiLink;

  activeTab = TabID.Dashboard;
}
