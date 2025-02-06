import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  ElementRef,
  Inject,
  inject, OnInit,
  ViewChild
} from '@angular/core';
import {ActivatedRoute, Router} from "@angular/router";
import {PersonService} from "../_services/person.service";
import {BehaviorSubject, EMPTY, Observable, switchMap, tap} from "rxjs";
import {Person, PersonRole} from "../_models/metadata/person";
import {AsyncPipe, NgStyle} from "@angular/common";
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
import {DefaultModalOptions} from "../_models/default-modal-options";
import {ToastrService} from "ngx-toastr";
import {LicenseService} from "../_services/license.service";
import {SafeUrlPipe} from "../_pipes/safe-url.pipe";

@Component({
  selector: 'app-person-detail',
  standalone: true,
  imports: [
    AsyncPipe,
    ImageComponent,
    SideNavCompanionBarComponent,
    ReadMoreComponent,
    TagBadgeComponent,
    PersonRolePipe,
    CarouselReelComponent,
    CardItemComponent,
    CardActionablesComponent,
    TranslocoDirective,
    ChapterCardComponent,
    SafeUrlPipe
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
  protected readonly licenseService = inject(LicenseService);
  private readonly themeService = inject(ThemeService);
  private readonly toastr = inject(ToastrService);

  protected readonly TagBadgeCursor = TagBadgeCursor;

  @ViewChild('scrollingBlock') scrollingBlock: ElementRef<HTMLDivElement> | undefined;
  @ViewChild('companionBar') companionBar: ElementRef<HTMLDivElement> | undefined;

  personName!: string;
  person: Person | null = null;
  roles$: Observable<PersonRole[]> | null = null;
  roles: PersonRole[] | null = null;
  works$: Observable<Series[]> | null = null;
  defaultSummaryText = 'No information about this Person';
  filter: SeriesFilterV2 | null = null;
  personActions: Array<ActionItem<Person>> = this.actionService.getPersonActions(this.handleAction.bind(this));
  chaptersByRole: any = {};
  anilistUrl: string = '';
  private readonly personSubject = new BehaviorSubject<Person | null>(null);
  protected readonly person$ = this.personSubject.asObservable().pipe(tap(p => {
    if (p?.aniListId) {
      this.anilistUrl = translate('person-detail.anilist-url').replace('{AniListId}', p!.aniListId! + '');
      this.cdRef.markForCheck();
    }
  }));

  get HasCoverImage() {
    return (this.person as Person).coverImage;
  }

  constructor() {
    this.route.paramMap.pipe(
      switchMap(params => {
        const personName = params.get('name');
        if (!personName) {
          this.router.navigateByUrl('/home');
          return EMPTY;
        }

        this.personName = personName;
        return this.personService.get(personName);
      }),
      tap((person) => {

        if (person == null) {
          this.toastr.error(translate('toasts.unauthorized-1'));
          this.router.navigateByUrl('/home');
          return;
        }

        this.person = person;
        this.personSubject.next(person); // emit the person data for subscribers
        this.themeService.setColorScape(person.primaryColor || '', person.secondaryColor);

        // Fetch roles and process them
        this.roles$ = this.personService.getRolesForPerson(this.person.id).pipe(
          tap(roles => {
            this.roles = roles;
            this.filter = this.createFilter(roles);
            this.chaptersByRole = {}; // Reset chaptersByRole for each person

            // Populate chapters by role
            roles.forEach(role => {
              this.chaptersByRole[role] = this.personService.getChaptersByRole(person.id, role)
                .pipe(takeUntilDestroyed(this.destroyRef));
            });
            this.cdRef.markForCheck();
          }),
          takeUntilDestroyed(this.destroyRef)
        );

        // Fetch series known for this person
        this.works$ = this.personService.getSeriesMostKnownFor(person.id).pipe(
          takeUntilDestroyed(this.destroyRef)
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
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


    loadPage(this.person!).subscribe();
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
        const ref = this.modalService.open(EditPersonModalComponent, DefaultModalOptions);
        ref.componentInstance.person = this.person;

        ref.closed.subscribe(r => {
          if (r.success) {
            const nameChanged = this.personName !== r.person.name;
            this.person = {...r.person};
            this.personName = this.person!.name;

            this.personSubject.next(this.person);

            // Update the url to reflect the new name change
            if (nameChanged) {
              const baseUrl = window.location.href.split('/').slice(0, -1).join('/');
              window.history.replaceState({}, '', `${baseUrl}/${encodeURIComponent(this.personName)}`);
            }

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
