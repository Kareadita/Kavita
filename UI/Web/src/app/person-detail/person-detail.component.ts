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
import {Observable, switchMap, tap} from "rxjs";
import {Person, PersonRole} from "../_models/metadata/person";
import {AsyncPipe, DOCUMENT, NgStyle} from "@angular/common";
import {ImageComponent} from "../shared/image/image.component";
import {ImageService} from "../_services/image.service";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {TagBadgeComponent, TagBadgeCursor} from "../shared/tag-badge/tag-badge.component";
import {PersonRolePipe} from "../_pipes/person-role.pipe";
import {CarouselReelComponent} from "../carousel/_components/carousel-reel/carousel-reel.component";
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {FilterComparison} from "../_models/metadata/v2/filter-comparison";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import {allPeople, personRoleForFilterField} from "../_models/metadata/v2/filter-field";
import {Series} from "../_models/series";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {FilterCombination} from "../_models/metadata/v2/filter-combination";
import {AccountService} from "../_services/account.service";
import {CardItemComponent} from "../cards/card-item/card-item.component";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";
import {Action, ActionFactoryService, ActionItem} from "../_services/action-factory.service";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {EditPersonModalComponent} from "./_modal/edit-person-modal/edit-person-modal.component";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ChapterCardComponent} from "../cards/chapter-card/chapter-card.component";
import {ThemeService} from "../_services/theme.service";

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
    SeriesCardComponent,
    CardItemComponent,
    CardActionablesComponent,
    TranslocoDirective,
    ChapterCardComponent
  ],
  templateUrl: './person-detail.component.html',
  styleUrl: './person-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PersonDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly personService = inject(PersonService);
  private readonly actionService = inject(ActionFactoryService);
  private readonly modalService = inject(NgbModal);
  protected readonly imageService = inject(ImageService);
  protected readonly accountService = inject(AccountService);
  private readonly themeService = inject(ThemeService);

  protected readonly TagBadgeCursor = TagBadgeCursor;

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;

  personName!: string;
  person$: Observable<Person> | null = null;
  person: Person | null = null;
  roles$: Observable<PersonRole[]> | null = null;
  roles: PersonRole[] | null = null;
  works$: Observable<Series[]> | null = null;
  defaultSummaryText = 'No information about this Person';
  filter: SeriesFilterV2 | null = null;
  personActions: Array<ActionItem<Person>> = this.actionService.getPersonActions(this.handleAction.bind(this));
  chaptersByRole: any = {};

  constructor(@Inject(DOCUMENT) private document: Document) {
    this.route.paramMap.subscribe(_ => {
      const personName = this.route.snapshot.paramMap.get('name');
      if (personName === null || undefined) {
        this.router.navigateByUrl('/home');
        return;
      }

      this.personName = personName;


      this.person$ = this.personService.get(this.personName).pipe(tap(p => {
        this.person = p;

        this.themeService.setColorScape(this.person.primaryColor || '', this.person.secondaryColor);

        this.roles$ = this.personService.getRolesForPerson(this.personName).pipe(tap(roles => {
          this.roles = roles;
          this.filter = this.createFilter(roles);

          for(let role of roles) {
            this.chaptersByRole[role] = this.personService.getChaptersByRole(this.person!.id, role).pipe(takeUntilDestroyed(this.destroyRef));
          }

          this.cdRef.markForCheck();
        }), takeUntilDestroyed(this.destroyRef));


        this.works$ = this.personService.getSeriesMostKnownFor(this.person.id).pipe(
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
      filter.statements.push({comparison: FilterComparison.Contains, value: this.person!.id + '', field: f});
    });

    return filter;
  }

  loadFilterByPerson() {
    const loadPage = (person: Person) => {
      // Create a filter of all roles with OR
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('person-detail.browse-person-title', {name: person.name});

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

  loadFilterByRole(role: PersonRole) {
    const personPipe = new PersonRolePipe();
    // Create a filter of all roles with OR
    const params: any = {};
    params['page'] = 1;
    params['title'] = translate('person-detail.browse-person-by-role-title', {name: this.person!.name, role: personPipe.transform(role)});

    const searchFilter = this.filterUtilityService.createSeriesV2Filter();
    searchFilter.limitTo = 0;
    searchFilter.combination = FilterCombination.Or;

    searchFilter.statements.push({comparison: FilterComparison.Contains, value: this.person!.id + '', field: personRoleForFilterField(role)});

    this.filterUtilityService.applyFilterWithParams(['all-series'], searchFilter, params).subscribe();
  }

  navigateToSeries(series: Series) {
    this.router.navigate(['library', series.libraryId, 'series', series.id]);
  }

  handleAction(action: ActionItem<Person>, person: Person) {
    switch (action.action) {
      case(Action.Edit):
        const ref = this.modalService.open(EditPersonModalComponent, {scrollable: true, size: 'lg', fullscreen: 'md'});
        ref.componentInstance.person = this.person;

        ref.closed.subscribe(r => {
          if (r.success) {
            this.person = {...r.person};
            this.cdRef.markForCheck();
          }
        });
        break;
      default:
        break;
    }
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action, this.person);
    }
  }
}
