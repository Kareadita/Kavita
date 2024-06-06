import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {JumpKey} from "../_models/jumpbar/jump-key";
import {EVENTS, Message, MessageHubService} from "../_services/message-hub.service";
import {translate, TranslocoDirective} from "@ngneat/transloco";
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

@Component({
  selector: 'app-all-filters',
  standalone: true,
  imports: [CommonModule, TranslocoDirective, CardItemComponent, SideNavCompanionBarComponent, CardDetailLayoutComponent, SafeHtmlPipe, CardActionablesComponent, RouterLink, FilterPipe, ManageSmartFiltersComponent],
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
  trackByIdentity = (index: number, item: SmartFilter) => item.name;

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

  async loadSmartFilter(filter: SmartFilter) {
    await this.router.navigateByUrl('all-series?' + filter.filter);
  }

  isErrored(filter: SmartFilter) {
    return !decodeURIComponent(filter.filter).includes('¦');
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

  protected readonly filter = filter;
}
