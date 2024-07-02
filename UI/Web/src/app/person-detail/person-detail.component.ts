import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  ElementRef,
  Inject,
  inject,
  ViewChild
} from '@angular/core';
import {ActivatedRoute, Router} from "@angular/router";
import {PersonService} from "../_services/person.service";
import {Observable, switchMap, tap, map} from "rxjs";
import {Person, PersonRole} from "../_models/metadata/person";
import {AsyncPipe, DOCUMENT, NgStyle} from "@angular/common";
import {ImageComponent} from "../shared/image/image.component";
import {ImageService} from "../_services/image.service";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {TagBadgeComponent} from "../shared/tag-badge/tag-badge.component";
import {PersonRolePipe} from "../_pipes/person-role.pipe";
import {CarouselReelComponent} from "../carousel/_components/carousel-reel/carousel-reel.component";
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {FilterComparison} from "../_models/metadata/v2/filter-comparison";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import {FilterField, allPeople} from "../_models/metadata/v2/filter-field";
import {Series} from "../_models/series";
import {SeriesService} from "../_services/series.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {PaginatedResult} from "../_models/pagination";
import {FilterCombination} from "../_models/metadata/v2/filter-combination";

@Component({
  selector: 'app-person-detail',
  standalone: true,
  imports: [
    AsyncPipe,
    ImageComponent,
    SideNavCompanionBarComponent,
    NgStyle,
    ReadMoreComponent,
    TagBadgeComponent,
    PersonRolePipe,
    CarouselReelComponent,
    SeriesCardComponent
  ],
  templateUrl: './person-detail.component.html',
  styleUrl: './person-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PersonDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly seriesService = inject(SeriesService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly personService = inject(PersonService);
  protected readonly imageService = inject(ImageService);

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;

  personId!: number;
  person$: Observable<Person> | null = null;
  person: Person | null = null;
  roles$: Observable<PersonRole[]> | null = null;
  roles: PersonRole[] | null = null;
  works$: Observable<Series[]> | null = null;
  defaultSummaryText = 'No information about this Person';
  filter: SeriesFilterV2 | null = null;

  get ScrollingBlockHeight() {
    if (this.scrollingBlock === undefined) return 'calc(var(--vh)*100)';
    const navbar = this.document.querySelector('.navbar') as HTMLElement;
    if (navbar === null) return 'calc(var(--vh)*100)';

    const companionHeight = this.companionBar!.nativeElement.offsetHeight;
    const navbarHeight = navbar.offsetHeight;
    const totalHeight = companionHeight + navbarHeight + 21; //21px to account for padding
    return 'calc(var(--vh)*100 - ' + totalHeight + 'px)';
  }

  constructor(@Inject(DOCUMENT) private document: Document) {
    this.route.paramMap.subscribe(_ => {
      const routeId = this.route.snapshot.paramMap.get('personId');
      if (routeId === null || undefined) {
        this.router.navigateByUrl('/home');
        return;
      }

      this.personId = parseInt(routeId, 10);
      this.person$ = this.personService.get(this.personId).pipe(tap(p => {
        this.person = p;

        this.cdRef.markForCheck();
      }), takeUntilDestroyed(this.destroyRef));

      this.roles$ = this.personService.getRolesForPerson(this.personId).pipe(tap(roles => {
        this.roles = roles;
        this.filter = this.createFilter(roles);

        this.works$ = this.seriesService.getSeriesForLibraryV2(undefined, undefined, this.filter!).pipe(
          map((d: PaginatedResult<Series[]>) => d.result),
          takeUntilDestroyed(this.destroyRef)
        );

        this.cdRef.markForCheck();
      }), takeUntilDestroyed(this.destroyRef));
    });
  }

  createFilter(roles: PersonRole[]) {
    const filter: SeriesFilterV2 = this.filterUtilityService.createSeriesV2Filter();
    filter.combination = FilterCombination.Or;
    filter.limitTo = 20;

    // I might want to use roles$ to do all this
    allPeople.forEach(f => {
      filter.statements.push({comparison: FilterComparison.Contains, value: this.personId + '', field: f});
    });

    return filter;
  }

  loadFilterByPerson() {
    const loadPage = (person: Person) => {
      // Create a filter of all roles with OR
      const params: any = {};
      params['page'] = 1;
      params['title'] = 'All Works of ' + person.name;

      const searchFilter = {...this.filter!};
      searchFilter.limitTo = 0;

      return this.filterUtilityService.applyFilterWithParams(['all-series'], searchFilter, params);
    };


    if (this.person) {
      loadPage(this.person).subscribe();
    } else {
      this.person$?.pipe(switchMap((p: Person) => {
        return loadPage(p);
      })).subscribe();
    }
  }
}
