import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, EventEmitter, inject} from '@angular/core';
import {
  SideNavCompanionBarComponent
} from "../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {CardDetailLayoutComponent} from "../../cards/card-detail-layout/card-detail-layout.component";
import {DecimalPipe} from "@angular/common";
import {Pagination} from "../../_models/pagination";
import {JumpKey} from "../../_models/jumpbar/jump-key";
import {ActivatedRoute, Router} from "@angular/router";
import {PersonService} from "../../_services/person.service";
import {BrowsePerson} from "../../_models/metadata/browse/browse-person";
import {JumpbarService} from "../../_services/jumpbar.service";
import {PersonCardComponent} from "../../cards/person-card/person-card.component";
import {ImageService} from "../../_services/image.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";
import {ReactiveFormsModule} from "@angular/forms";
import {PersonSortField} from "../../_models/metadata/v2/person-sort-field";
import {PersonFilterField} from "../../_models/metadata/v2/person-filter-field";
import {FilterUtilitiesService} from "../../shared/_services/filter-utilities.service";
import {FilterV2} from "../../_models/metadata/v2/filter-v2";
import {PersonFilterSettings} from "../../metadata-filter/filter-settings";
import {FilterEvent} from "../../_models/metadata/series-filter";
import {PersonRole} from "../../_models/metadata/person";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {MetadataService} from "../../_services/metadata.service";
import {FilterStatement} from "../../_models/metadata/v2/filter-statement";


@Component({
  selector: 'app-browse-people',
  imports: [
    SideNavCompanionBarComponent,
    TranslocoDirective,
    CardDetailLayoutComponent,
    DecimalPipe,
    PersonCardComponent,
    CompactNumberPipe,
    ReactiveFormsModule,

  ],
  templateUrl: './browse-people.component.html',
  styleUrl: './browse-people.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BrowsePeopleComponent {
  protected readonly PersonSortField = PersonSortField;

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly personService = inject(PersonService);
  private readonly jumpbarService = inject(JumpbarService);
  private readonly route = inject(ActivatedRoute);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly imageService = inject(ImageService);
  protected readonly metadataService = inject(MetadataService);

  isLoading = false;
  authors: Array<BrowsePerson> = [];
  pagination: Pagination = {currentPage: 0, totalPages: 0, totalItems: 0, itemsPerPage: 0};
  refresh: EventEmitter<void> = new EventEmitter();
  jumpKeys: Array<JumpKey> = [];
  trackByIdentity = (index: number, item: BrowsePerson) => `${item.id}`;
  filterSettings: PersonFilterSettings = new PersonFilterSettings();
  filterActive: boolean = false;
  filterOpen: EventEmitter<boolean> = new EventEmitter();
  filter: FilterV2<PersonFilterField, PersonSortField> | undefined = undefined;
  filterActiveCheck!: FilterV2<PersonFilterField>;


  constructor() {
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.route.data.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
      this.filter = data['filter'] as FilterV2<PersonFilterField, PersonSortField>;

      if (this.filter == null) {
        this.filter = this.metadataService.createDefaultFilterDto('person');
        this.filter.statements.push(this.metadataService.createDefaultFilterStatement('person') as FilterStatement<PersonFilterField>);
      }

      this.filterActiveCheck = this.filterUtilityService.createPersonV2Filter();
      this.filterActiveCheck!.statements.push({value: `${PersonRole.Writer},${PersonRole.CoverArtist}`, field: PersonFilterField.Role, comparison: FilterComparison.Contains});
      this.filterSettings.presetsV2 = this.filter;

      this.cdRef.markForCheck();
      this.loadData();
    });
  }


  loadData() {
    if (!this.filter) {
      this.filter = this.metadataService.createDefaultFilterDto('person');
      this.filter.statements.push(this.metadataService.createDefaultFilterStatement('person') as FilterStatement<PersonFilterField>);
      this.cdRef.markForCheck();
    }

    this.personService.getAuthorsToBrowse(this.filter!).subscribe(d => {
      this.authors = [...d.result];
      this.pagination = d.pagination;
      this.jumpKeys = this.jumpbarService.getJumpKeys(this.authors, d => d.name);
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  goToPerson(person: BrowsePerson) {
    this.router.navigate(['person', person.name]);
  }

  updateFilter(data: FilterEvent<PersonFilterField, PersonSortField>) {
    if (data.filterV2 === undefined) return;
    this.filter = data.filterV2;

    if (data.isFirst) {
      this.loadData();
      return;
    }

    this.filterUtilityService.updateUrlFromFilter(this.filter).subscribe((_) => {
      this.loadData();
    });
  }
}
