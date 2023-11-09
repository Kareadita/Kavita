import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {JumpKey} from "../_models/jumpbar/jump-key";
import {EVENTS, Message, MessageHubService} from "../_services/message-hub.service";
import {TranslocoDirective} from "@ngneat/transloco";
import {CardItemComponent} from "../cards/card-item/card-item.component";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {SmartFilter} from "../_models/metadata/v2/smart-filter";
import {FilterService} from "../_services/filter.service";
import {CardDetailLayoutComponent} from "../cards/card-detail-layout/card-detail-layout.component";
import {SafeHtmlPipe} from "../_pipes/safe-html.pipe";
import {Router} from "@angular/router";

@Component({
  selector: 'app-all-filters',
  standalone: true,
  imports: [CommonModule, TranslocoDirective, CardItemComponent, SideNavCompanionBarComponent, CardDetailLayoutComponent, SafeHtmlPipe],
  templateUrl: './all-filters.component.html',
  styleUrl: './all-filters.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AllFiltersComponent implements OnInit {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly hubService = inject(MessageHubService);
  private readonly router = inject(Router);
  private readonly filterService = inject(FilterService);

  jumpbarKeys: Array<JumpKey> = [];
  filters: SmartFilter[] = [];
  isLoading = true;
  trackByIdentity = (index: number, item: SmartFilter) => item.name;

  ngOnInit() {
    this.filterService.getAllFilters().subscribe(filters => {
      this.filters = filters;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
    // this.hubService.messages$.pipe(debounceTime(6000), takeUntilDestroyed(this.destroyRef)).subscribe((event: Message<any>) => {
    //   if (event.event !== EVENTS.) return;
    //   this.loadPage();
    // });
  }

  async loadSmartFilter(filter: SmartFilter) {
    await this.router.navigateByUrl('all-series?' + filter.filter);
  }

}
