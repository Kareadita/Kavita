import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {JumpKey} from "../_models/jumpbar/jump-key";
import {TranslocoDirective} from "@jsverse/transloco";
import {CardItemComponent} from "../cards/card-item/card-item.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {SmartFilter} from "../_models/metadata/v2/smart-filter";
import {FilterService} from "../_services/filter.service";
import {CardDetailLayoutComponent} from "../cards/card-detail-layout/card-detail-layout.component";
import {SafeHtmlPipe} from "../_pipes/safe-html.pipe";
import {Router, RouterLink} from "@angular/router";
import {Series} from "../_models/series";
import {JumpbarService} from "../_services/jumpbar.service";
import {Action, ActionFactoryService, ActionItem} from "../_services/action-factory.service";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";
import {ActionService} from "../_services/action.service";
import {FilterPipe} from "../_pipes/filter.pipe";
import {filter} from "rxjs";
import {ManageSmartFiltersComponent} from "../sidenav/_components/manage-smart-filters/manage-smart-filters.component";
import {DecimalPipe} from "@angular/common";

@Component({
  selector: 'app-all-filters',
  standalone: true,
  imports: [TranslocoDirective, CardItemComponent, SideNavCompanionBarComponent, CardDetailLayoutComponent, SafeHtmlPipe, CardActionablesComponent, RouterLink, FilterPipe, ManageSmartFiltersComponent, DecimalPipe],
  templateUrl: './all-filters.component.html',
  styleUrl: './all-filters.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AllFiltersComponent implements OnInit {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly router = inject(Router);
  private readonly filterService = inject(FilterService);
  private readonly actionFactory = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);

  filterActions = this.actionFactory.getSmartFilterActions(this.handleAction.bind(this));
  jumpbarKeys: Array<JumpKey> = [];
  filters: SmartFilter[] = [];
  isLoading = true;

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.filterService.getAllFilters().subscribe(filters => {
      this.filters = filters;
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(this.filters, (s: Series) => s.name);
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  async deleteFilter(filter: SmartFilter) {
    await this.actionService.deleteFilter(filter.id, success => {
      this.filters = this.filters.filter(f => f.id != filter.id);
      this.jumpbarKeys = this.jumpbarService.getJumpKeys(this.filters, (s: Series) => s.name);
      this.cdRef.markForCheck();
    });
  }

  async handleAction(action: ActionItem<SmartFilter>, filter: SmartFilter) {
    switch (action.action) {
      case(Action.Delete):
        await this.deleteFilter(filter);
        break;
      default:
        break;
    }
  }
}
