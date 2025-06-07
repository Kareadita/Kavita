import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  OnInit
} from '@angular/core';
import {
  SideNavCompanionBarComponent
} from "../../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {CardDetailLayoutComponent} from "../../cards/card-detail-layout/card-detail-layout.component";
import {DecimalPipe} from "@angular/common";
import {Series} from "../../_models/series";
import {Pagination} from "../../_models/pagination";
import {JumpKey} from "../../_models/jumpbar/jump-key";
import {Router} from "@angular/router";
import {PersonService} from "../../_services/person.service";
import {BrowsePerson} from "../../_models/metadata/browse/browse-person";
import {JumpbarService} from "../../_services/jumpbar.service";
import {PersonCardComponent} from "../../cards/person-card/person-card.component";
import {ImageService} from "../../_services/image.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {CompactNumberPipe} from "../../_pipes/compact-number.pipe";
import {allPeopleRoles, PersonRole} from "../../_models/metadata/person";
import {Select2} from "ng-select2-component";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {PersonRolePipe} from "../../_pipes/person-role.pipe";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, tap} from "rxjs/operators";
import {SortButtonComponent} from "../../_single-module/sort-button/sort-button.component";
import {PersonSortField} from "../../_models/metadata/v2/person-sort-field";
import {PersonSortOptions} from "../../_models/metadata/v2/sort-options";


@Component({
  selector: 'app-browse-authors',
  imports: [
    SideNavCompanionBarComponent,
    TranslocoDirective,
    CardDetailLayoutComponent,
    DecimalPipe,
    PersonCardComponent,
    CompactNumberPipe,
    Select2,
    ReactiveFormsModule,
    SortButtonComponent,
  ],
  templateUrl: './browse-authors.component.html',
  styleUrl: './browse-authors.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BrowseAuthorsComponent implements OnInit {
  protected readonly PersonSortField = PersonSortField;

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly personService = inject(PersonService);
  private readonly jumpbarService = inject(JumpbarService);
  protected readonly imageService = inject(ImageService);


  series: Series[] = [];
  isLoading = false;
  authors: Array<BrowsePerson> = [];
  pagination: Pagination = {currentPage: 0, totalPages: 0, totalItems: 0, itemsPerPage: 0};
  refresh: EventEmitter<void> = new EventEmitter();
  jumpKeys: Array<JumpKey> = [];
  trackByIdentity = (index: number, item: BrowsePerson) => `${item.id}`;
  personRolePipe = new PersonRolePipe();
  allRoles = allPeopleRoles.map(r => {return {value: r, label: this.personRolePipe.transform(r)}});
  filterGroup = new FormGroup({
    roles: new FormControl([PersonRole.CoverArtist, PersonRole.Writer], []),
    sortField: new FormControl(PersonSortField.Name, []),
    query: new FormControl('', []),
  });
  isAscending:  boolean = true;


  ngOnInit() {
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.filterGroup.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef),
      debounceTime(200),
      tap(_ => this.loadData())
    ).subscribe()

    this.loadData();
  }

  onSortUpdate(isAscending: boolean) {
    this.isAscending = isAscending;
    this.loadData();
  }

  loadData() {
    const roles = this.filterGroup.get('roles')?.value ?? [];
    const sortOptions = {sortField: parseInt(this.filterGroup.get('sortField')!.value + '', 10), isAscending: this.isAscending} as PersonSortOptions;
    const query = this.filterGroup.get('query')?.value ?? '';

    this.personService.getAuthorsToBrowse({roles, sortOptions, query}).subscribe(d => {
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
}
