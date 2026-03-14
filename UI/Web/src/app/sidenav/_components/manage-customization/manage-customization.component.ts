import {ChangeDetectionStrategy, Component} from '@angular/core';
import {CustomizeDashboardStreamsComponent} from "../customize-dashboard-streams/customize-dashboard-streams.component";
import {CustomizeSidenavStreamsComponent} from "../customize-sidenav-streams/customize-sidenav-streams.component";
import {ManageExternalSourcesComponent} from "../manage-external-sources/manage-external-sources.component";
import {ManageSmartFiltersComponent} from "../manage-smart-filters/manage-smart-filters.component";
import {NgbNav, NgbNavContent, NgbNavItem, NgbNavLink, NgbNavOutlet} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {WikiLink} from 'src/app/_models/wiki';
import {Tabs} from "../../../_models/tabs";
import {TabTitlePipe} from "../../../_pipes/tab-title.pipe";


@Component({
    selector: 'app-manage-customization',
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
    NgbNavOutlet,
    TabTitlePipe
  ],
    templateUrl: './manage-customization.component.html',
    styleUrl: './manage-customization.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageCustomizationComponent {

  activeTab = Tabs.Dashboard;

  protected readonly Tabs = Tabs;
  protected readonly WikiLink = WikiLink;
}
