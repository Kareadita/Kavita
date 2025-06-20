import {ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, inject, OnInit} from '@angular/core';
import {CardDetailLayoutComponent} from "../../cards/card-detail-layout/card-detail-layout.component";
import {DecimalPipe} from "@angular/common";
import {
  SideNavCompanionBarComponent
} from "../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {MetadataService} from "../../_services/metadata.service";
import {JumpbarService} from "../../_services/jumpbar.service";
import {FilterUtilitiesService} from "../../shared/_services/filter-utilities.service";
import {BrowseGenre} from "../../_models/metadata/browse/browse-genre";
import {Pagination} from "../../_models/pagination";
import {JumpKey} from "../../_models/jumpbar/jump-key";
import {BrowsePerson} from "../../_models/metadata/browse/browse-person";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {BrowseTag} from "../../_models/metadata/browse/browse-tag";
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";
import {Title} from "@angular/platform-browser";

@Component({
  selector: 'app-browse-tags',
  imports: [
    CardDetailLayoutComponent,
    DecimalPipe,
    SideNavCompanionBarComponent,
    TranslocoDirective,
    CompactNumberPipe
  ],
  templateUrl: './browse-tags.component.html',
  styleUrl: './browse-tags.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BrowseTagsComponent implements OnInit {
  protected readonly FilterField = FilterField;

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  private readonly jumpbarService = inject(JumpbarService);
  protected readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly titleService = inject(Title);

  isLoading = false;
  tags: Array<BrowseTag> = [];
  pagination: Pagination = {currentPage: 0, totalPages: 0, totalItems: 0, itemsPerPage: 0};
  refresh: EventEmitter<void> = new EventEmitter();
  jumpKeys: Array<JumpKey> = [];
  trackByIdentity = (index: number, item: BrowsePerson) => `${item.id}`;

  ngOnInit() {
    this.isLoading = true;
    this.cdRef.markForCheck();
    this.titleService.setTitle('Kavita - ' + translate('browse-tags.title'));

    this.metadataService.getTagWithCounts(undefined, undefined).subscribe(d => {
      this.tags = d.result;
      this.pagination = d.pagination;
      this.jumpKeys = this.jumpbarService.getJumpKeys(this.tags, (d: BrowseGenre) => d.title);
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  openFilter(field: FilterField, value: string | number) {
    this.filterUtilityService.applyFilter(['all-series'], field, FilterComparison.Equal, `${value}`).subscribe();
  }
}
