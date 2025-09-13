import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {JumpKey} from "../_models/jumpbar/jump-key";
import {TranslocoDirective} from "@jsverse/transloco";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {SmartFilter} from "../_models/metadata/v2/smart-filter";
import {FilterService} from "../_services/filter.service";
import {Router} from "@angular/router";
import {Series} from "../_models/series";
import {JumpbarService} from "../_services/jumpbar.service";
import {ActionFactoryService} from "../_services/action-factory.service";
import {ActionService} from "../_services/action.service";
import {ManageSmartFiltersComponent} from "../sidenav/_components/manage-smart-filters/manage-smart-filters.component";
import {APP_BASE_HREF, DecimalPipe} from "@angular/common";

@Component({
  selector: 'app-all-filters',
  imports: [TranslocoDirective, SideNavCompanionBarComponent, ManageSmartFiltersComponent, DecimalPipe],
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
  protected readonly baseUrl = inject(APP_BASE_HREF);


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
}
