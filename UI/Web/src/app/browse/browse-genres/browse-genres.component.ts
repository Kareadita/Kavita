import {ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, inject, OnInit} from '@angular/core';
import {CardDetailLayoutComponent} from "../../cards/card-detail-layout/card-detail-layout.component";
import {DecimalPipe} from "@angular/common";
import {
  SideNavCompanionBarComponent
} from "../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {JumpbarService} from "../../_services/jumpbar.service";
import {BrowsePerson} from "../../_models/metadata/browse/browse-person";
import {Pagination} from "../../_models/pagination";
import {JumpKey} from "../../_models/jumpbar/jump-key";
import {MetadataService} from "../../_services/metadata.service";
import {BrowseGenre} from "../../_models/metadata/browse/browse-genre";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {FilterUtilitiesService} from "../../shared/_services/filter-utilities.service";
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";
import {Title} from "@angular/platform-browser";

@Component({
  selector: 'app-browse-genres',
  imports: [
    CardDetailLayoutComponent,
    DecimalPipe,
    SideNavCompanionBarComponent,
    TranslocoDirective,
    CompactNumberPipe
  ],
  templateUrl: './browse-genres.component.html',
  styleUrl: './browse-genres.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BrowseGenresComponent implements OnInit {

  protected readonly FilterField = FilterField;

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly metadataService = inject(MetadataService);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly titleService = inject(Title);

  isLoading = false;
  genres: Array<BrowseGenre> = [];
  pagination: Pagination = {currentPage: 0, totalPages: 0, totalItems: 0, itemsPerPage: 0};
  refresh: EventEmitter<void> = new EventEmitter();
  jumpKeys: Array<JumpKey> = [];
  trackByIdentity = (index: number, item: BrowsePerson) => `${item.id}`;

  ngOnInit() {
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.titleService.setTitle('Kavita - ' + translate('browse-genres.title'));

    this.metadataService.getGenreWithCounts(undefined, undefined).subscribe(d => {
      this.genres = d.result;
      this.pagination = d.pagination;
      this.jumpKeys = this.jumpbarService.getJumpKeys(this.genres, (d: BrowseGenre) => d.title);
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  openFilter(field: FilterField, value: string | number) {
    this.filterUtilityService.applyFilter(['all-series'], field, FilterComparison.Equal, `${value}`).subscribe();
  }
}
