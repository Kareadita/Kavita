import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal} from '@angular/core';
import {ActivatedRoute, Router} from "@angular/router";
import {PersonService} from "../_services/person.service";
import {Observable} from "rxjs";
import {Person, PersonRole} from "../_models/metadata/person";
import {AsyncPipe} from "@angular/common";
import {ImageComponent} from "../shared/image/image.component";
import {ImageService} from "../_services/image.service";
import {
  SideNavCompanionBarComponent
} from "../sidenav/_components/side-nav-companion-bar/side-nav-companion-bar.component";
import {ReadMoreComponent} from "../shared/read-more/read-more.component";
import {PersonRolePipe} from "../_pipes/person-role.pipe";
import {CarouselReelComponent} from "../carousel/_components/carousel-reel/carousel-reel.component";
import {FilterComparison} from "../_models/metadata/v2/filter-comparison";
import {FilterUtilitiesService} from "../shared/_services/filter-utilities.service";
import {FilterV2} from "../_models/metadata/v2/filter-v2";
import {FilterField, personRoleForFilterField} from "../_models/metadata/v2/filter-field";
import {Series} from "../_models/series";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {FilterCombination} from "../_models/metadata/v2/filter-combination";
import {AccountService} from "../_services/account.service";
import {CardActionablesComponent} from "../_single-module/card-actionables/card-actionables.component";
import {ActionFactoryService} from "../_services/action-factory.service";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ChapterCardComponent} from "../cards/chapter-card/chapter-card.component";
import {ThemeService} from "../_services/theme.service";
import {ToastrService} from "ngx-toastr";
import {LicenseService} from "../_services/license.service";
import {SafeUrlPipe} from "../_pipes/safe-url.pipe";
import {EVENTS, MessageHubService} from "../_services/message-hub.service";
import {BadgeExpanderComponent} from "../shared/badge-expander/badge-expander.component";
import {MetadataService} from "../_services/metadata.service";
import {SeriesCardComponent} from "../cards/series-card/series-card.component";
import {ActionResult} from "../_models/actionables/action-result";
import {getWritableResolvedData} from "../../libs/route-util";
import {StandaloneChapter} from "../_models/standalone-chapter";

interface PersonMergeEvent {
  srcId: number,
  dstId: number,
  dstName: number,
}


@Component({
  selector: 'app-person-detail',
  imports: [
    AsyncPipe,
    ImageComponent,
    SideNavCompanionBarComponent,
    ReadMoreComponent,
    PersonRolePipe,
    CarouselReelComponent,
    CardActionablesComponent,
    TranslocoDirective,
    ChapterCardComponent,
    SafeUrlPipe,
    BadgeExpanderComponent,
    SeriesCardComponent
  ],
  templateUrl: './person-detail.component.html',
  styleUrl: './person-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PersonDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly personService = inject(PersonService);
  private readonly actionService = inject(ActionFactoryService);
  protected readonly imageService = inject(ImageService);
  protected readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);
  private readonly themeService = inject(ThemeService);
  private readonly toastr = inject(ToastrService);
  private readonly messageHubService = inject(MessageHubService)
  private readonly metadataService = inject(MetadataService)

  person = getWritableResolvedData(this.route, 'person');
  personName = computed(() => this.person().name);
  works = signal<Series[]>([]);
  filter = signal<FilterV2<FilterField> | null>(null);
  hasCoverImage = computed(() => this.person().coverImage);
  personActions = this.actionService.getPersonActions();

  chaptersByRole = computed(() => {
    const p = this.person();
    if (!p?.roles) return {} as Record<number, Observable<StandaloneChapter[]>>;
    const result: Record<number, Observable<StandaloneChapter[]>> = {};
    p.roles.forEach(role => {
      result[role] = this.personService.getChaptersByRole(p.id, role);
    });
    return result;
  });



  constructor() {
    this.messageHubService.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(message => {
      if (message.event !== EVENTS.PersonMerged) return;

      const event = message.payload as PersonMergeEvent;
      if (event.srcId !== this.person().id) return;

      this.router.navigate(['person', event.dstName]);
    });

    this.setPerson(this.person());
  }

  private setPerson(person: Person) {
    this.person.set(person);
    this.themeService.setColorScape(person.primaryColor || '', person.secondaryColor);

    const roles = person.roles;
    if (roles) {
      this.filter.set(this.createFilter(roles));
    }

    // Fetch series known for this person
    this.personService.getSeriesMostKnownFor(person.id).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(series => this.works.set(series));
  }

  createFilter(roles: PersonRole[]) {
    const filter = this.metadataService.createDefaultFilterDto('series');
    filter.combination = FilterCombination.Or;
    filter.limitTo = 20;

    roles.forEach(pr => {
      filter.statements.push({comparison: FilterComparison.Contains, value: this.person().id + '', field: personRoleForFilterField(pr)});
    });

    return filter;
  }

  loadFilterByPerson() {
    const loadPage = (person: Person) => {
      // Create a filter of all roles with OR
      const params: any = {};
      params['page'] = 1;
      params['title'] = translate('person-detail.browse-person-title', {name: person.name});

      const searchFilter = {...this.filter()!};
      searchFilter.limitTo = 0;

      return this.filterUtilityService.applyFilterWithParams(['all-series'], searchFilter, params);
    };


    loadPage(this.person()).subscribe();
  }

  loadFilterByRole(role: PersonRole) {
    const personPipe = new PersonRolePipe();
    // Create a filter of all roles with OR
    const params: any = {};
    params['page'] = 1;
    params['title'] = translate('person-detail.browse-person-by-role-title', {name: this.person().name, role: personPipe.transform(role)});

    const searchFilter = this.metadataService.createDefaultFilterDto('series');
    searchFilter.limitTo = 0;
    searchFilter.combination = FilterCombination.Or;

    searchFilter.statements.push({comparison: FilterComparison.Contains, value: this.person().id + '', field: personRoleForFilterField(role)});

    this.filterUtilityService.applyFilterWithParams(['all-series'], searchFilter, params).subscribe();
  }

  handleActionCallback(event: ActionResult<Person>) {
    const result = event as unknown as ActionResult<Person>;

    switch (result.effect) {
      case 'update':
      case 'reload':
        const oldName = this.personName();

        // Reload person as the web links (and roles) may have changed
        this.personService.get(result.entity.name).subscribe(person => {
          this.setPerson(person!);

          // Update the url to reflect the new name change
          if (oldName !== person!.name) {
            const baseUrl = window.location.href.split('/').slice(0, -1).join('/');
            window.history.replaceState({}, '', `${baseUrl}/${encodeURIComponent(person!.name)}`);
          }
        });
        break;
      case 'remove':
      case 'none':
        break;
    }
  }

  protected readonly FilterField = FilterField;
}
