import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule} from "@angular/forms";
import {filter, map, of, switchMap, tap} from "rxjs";
import {AgeRating, AgeRatings} from "../../_models/metadata/age-rating";
import {
  ReadStatusTransitionRule,
  ReviewScrobbleTarget,
  ReviewScrobbleTargets,
  ScrobbleProviderSettings,
  ScrobbleReadStatus,
  ScrobbleReadStatuses,
  UserScrobbleProvider
} from "../../_models/kavitaplus/scrobble-provider-settings";
import {PublicationStatus, PublicationStatuses} from "../../_models/metadata/publication-status";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged} from "rxjs/operators";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AccordionComponent} from "../../shared/accordion/accordion.component";
import {ScrobbleProviderNamePipe} from "../../_pipes/scrobble-provider-name.pipe";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {ScrobbleProviderDescriptionPipe} from "../manga-user-preferences/scrobble-provider-description.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {ScrobbleEventType} from "../../_models/scrobbling/scrobble-event";
import {ReviewScrobbleTargetNamePipe} from "../../_pipes/review-scrobble-target-name.pipe";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {Library, LibraryType} from "../../_models/library/library";
import {LibraryService} from "../../_services/library.service";
import {PublicationStatusPipe} from "../../_pipes/publication-status.pipe";
import {ScrobbleReadStatusPipe} from "../../_pipes/scrobble-read-status.pipe";
import {Select2, Select2Data} from "ng-select2-component";
import {TypeaheadSettings} from "../../typeahead/_models/typeahead-settings";
import {TypeaheadComponent} from "../../typeahead/_components/typeahead.component";
import {ModalService} from "../../_services/modal.service";
import {
  ManageUserScrobbleProviderModalComponent
} from "../_modals/manage-user-scrobble-provider-modal/manage-user-scrobble-provider-modal.component";
import {ConfirmService} from "../../shared/confirm.service";
import {fromPromise} from "rxjs/internal/observable/innerFrom";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {ScrobbleProviderUpdatedEvent} from "../../_models/events/scrobble-provider-updated-event";
import {NgOptimizedImage} from "@angular/common";

type ReadStatusTransitionRuleFromGroup = FormGroup<{
  enabled: FormControl<boolean>;
  days: FormControl<number>;
  transitionStatus: FormControl<ScrobbleReadStatus>;
  excludedPublicationStatus: FormControl<PublicationStatus[]>;
}>;

type ScrobbleProviderSettingsFormGroup = FormGroup<{
  progressScrobbling: FormControl<boolean>;
  wantToReadSync: FormControl<boolean>;
  ratingScrobbling: FormControl<boolean>;
  reviewsScrobbling: FormControl<boolean>;
  reviewScrobbleTarget: FormControl<ReviewScrobbleTarget>;
  allLibraries: FormControl<boolean>;
  libraries: FormControl<number[]>;
  highestAgeRating:FormControl<AgeRating>;
  inactiveSeriesRule: ReadStatusTransitionRuleFromGroup;
  droppedSeriesRule: ReadStatusTransitionRuleFromGroup;
}>;

const ProviderSupportedEvents: Record<ScrobbleProvider, ScrobbleEventType[]> = {
  [ScrobbleProvider.AniList]: [ScrobbleEventType.ScoreUpdated, ScrobbleEventType.Review, ScrobbleEventType.ChapterRead, ScrobbleEventType.AddWantToRead],
  [ScrobbleProvider.Hardcover]: [ScrobbleEventType.ScoreUpdated, ScrobbleEventType.Review, ScrobbleEventType.ChapterRead, ScrobbleEventType.AddWantToRead],
  [ScrobbleProvider.Mal]: [ScrobbleEventType.AddWantToRead],
  [ScrobbleProvider.MangaBaka]: [ScrobbleEventType.ScoreUpdated, ScrobbleEventType.Review, ScrobbleEventType.ChapterRead, ScrobbleEventType.AddWantToRead],
  [ScrobbleProvider.Cbr]: [],
  [ScrobbleProvider.Kavita]: []
}

const ProvidersSupportLibraryTypes: Record<ScrobbleProvider, LibraryType[]> = {
  [ScrobbleProvider.AniList]: [LibraryType.Manga, LibraryType.LightNovel],
  [ScrobbleProvider.Hardcover]: [LibraryType.LightNovel, LibraryType.Book],
  [ScrobbleProvider.Mal]: [LibraryType.Manga, LibraryType.LightNovel],
  [ScrobbleProvider.MangaBaka]: [LibraryType.Manga, LibraryType.LightNovel],
  [ScrobbleProvider.Cbr]: [LibraryType.Comic],
  [ScrobbleProvider.Kavita]: []
}

@Component({
  selector: 'app-manage-scrobble-providers',
  imports: [
    TranslocoDirective,
    AccordionComponent,
    ScrobbleProviderNamePipe,
    ProviderImagePipe,
    ReactiveFormsModule,
    TagBadgeComponent,
    ScrobbleProviderDescriptionPipe,
    UtcToLocalTimePipe,
    ReviewScrobbleTargetNamePipe,
    AgeRatingPipe,
    ScrobbleReadStatusPipe,
    Select2,
    TypeaheadComponent,
    LoadingComponent,
    NgOptimizedImage
  ],
  templateUrl: './manage-scrobble-providers.component.html',
  styleUrl: './manage-scrobble-providers.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageScrobbleProvidersComponent implements OnInit {

  protected readonly scrobbleService = inject(ScrobblingService);
  private readonly libraryService = inject(LibraryService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly destroyRef$ = inject(DestroyRef);
  private readonly modalService = inject(ModalService);
  private readonly confirmService = inject(ConfirmService);
  private readonly messageHub = inject(MessageHubService);

  formGroups = signal<Map<ScrobbleProvider, ScrobbleProviderSettingsFormGroup>>(new Map());
  userScrobbleProviders = signal<Map<ScrobbleProvider, UserScrobbleProvider>>(new Map());
  loading = computed(() => this.formGroups().size === 0);

  libraries = signal<Library[]>([]);

  private publicationStatusPipe = new PublicationStatusPipe();
  private scrobbleProviderNamePipe = new ScrobbleProviderNamePipe();

  publicationStatusOptions: Select2Data = PublicationStatuses.map(p => ({
    value: p,
    label: this.publicationStatusPipe.transform(p)
  }));

  ngOnInit() {
    this.libraryService.getLibraries().subscribe(libraries => this.libraries.set(libraries));

    this.loadData().subscribe();

    this.messageHub.messages$.pipe(
      filter(msg => msg.event === EVENTS.ScrobbleProviderUpdated),
      map(msg => (msg.payload as ScrobbleProviderUpdatedEvent).provider),
      switchMap(() => this.loadData())
    ).subscribe();
  }

  private loadData() {
    return this.scrobbleService.getScrobbleProviders()
      .pipe(tap(userScrobbleProviders => {
        const groups: Map<ScrobbleProvider, ScrobbleProviderSettingsFormGroup> = new Map();

        for (const p of userScrobbleProviders) {
          const group = this.buildScrobbleProviderSettingsFormGroup(p.settings);

          groups.set(p.provider, group);

          this.listenToChanges(p.provider, group);
        }

        this.userScrobbleProviders.set(new Map(userScrobbleProviders.map(p => [p.provider, p])));
        this.formGroups.set(groups);
      }));
  }

  private listenToChanges(provider: ScrobbleProvider, group: ScrobbleProviderSettingsFormGroup) {
    group.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef$),
      distinctUntilChanged(),
      debounceTime(500),
      map(() => {
        const userScrobbleProvider = this.userScrobbleProviders().get(provider)!;

        return {
          ...userScrobbleProvider,
          settings: group.getRawValue(),
        }
      }),
      switchMap(s => this.scrobbleService.saveScrobbleSettings(s))
    ).subscribe();
  }

  private buildScrobbleProviderSettingsFormGroup(scrobbleSettings: ScrobbleProviderSettings): ScrobbleProviderSettingsFormGroup {
    return this.fb.group({
      progressScrobbling: this.fb.control(scrobbleSettings.progressScrobbling),
      wantToReadSync: this.fb.control(scrobbleSettings.wantToReadSync),
      ratingScrobbling: this.fb.control(scrobbleSettings.ratingScrobbling),
      reviewsScrobbling: this.fb.control(scrobbleSettings.reviewsScrobbling),
      reviewScrobbleTarget: this.fb.control(scrobbleSettings.reviewScrobbleTarget),
      allLibraries: this.fb.control(scrobbleSettings.allLibraries),
      libraries: this.fb.control(scrobbleSettings.libraries),
      highestAgeRating: this.fb.control(scrobbleSettings.highestAgeRating),
      inactiveSeriesRule: this.buildReadStatusTransitionRuleFromGroup(scrobbleSettings.inactiveSeriesRule),
      droppedSeriesRule: this.buildReadStatusTransitionRuleFromGroup(scrobbleSettings.droppedSeriesRule),
    });
  }

  private buildReadStatusTransitionRuleFromGroup(rule: ReadStatusTransitionRule): ReadStatusTransitionRuleFromGroup {
    return this.fb.group({
      enabled: this.fb.control(rule.enabled),
      days: this.fb.control(rule.days),
      transitionStatus: this.fb.control(rule.transitionStatus),
      excludedPublicationStatus: this.fb.control(rule.excludedPublicationStatus),
    })
  }

  protected inactiveSeriesRule(formGroup: ScrobbleProviderSettingsFormGroup): ReadStatusTransitionRuleFromGroup {
    return formGroup.get('inactiveSeriesRule') as ReadStatusTransitionRuleFromGroup;
  }

  protected droppedSeriesRule(formGroup: ScrobbleProviderSettingsFormGroup): ReadStatusTransitionRuleFromGroup {
    return formGroup.get('droppedSeriesRule') as ReadStatusTransitionRuleFromGroup;
  }

  protected libraryTypeaheadSettings(provider: ScrobbleProvider): TypeaheadSettings<Library> {
    const libraries = this.libraries()
      .filter(l => ProvidersSupportLibraryTypes[provider].includes(l.type));

    const userScrobbleProvider = this.userScrobbleProviders().get(provider)!;

    const settings = new TypeaheadSettings<Library>();
    settings.id = "libraries-" + provider;
    settings.multiple = true;
    settings.fetchFn = () => of(libraries);
    settings.compareFn = (optionList, filter) => optionList.filter(item => item.name.toLowerCase().includes(filter.toLowerCase()));
    settings.savedData = libraries.filter(l => userScrobbleProvider.settings.libraries.includes(l.id));
    settings.trackByIdentityFn = (index, value) => value.id + '';
    settings.minCharacters = 0; // All preloaded

    return settings;
  }

  updateLibrarySelection(provider: ScrobbleProvider, libraries: Library[]) {
    const group = this.formGroups().get(provider);

    group?.get('libraries')?.setValue(libraries.map(l => l.id));
  }

  protected async disconnectScrobbleProvider(provider: ScrobbleProvider) {
    const userScrobbleProvider = this.userScrobbleProviders().get(provider);
    if (!userScrobbleProvider) return;

    fromPromise(this.confirmService.confirm(translate('scrobble-provider-settings-manager.confirm-delete',
      {provider: this.scrobbleProviderNamePipe.transform(provider)}))).pipe(
      filter(confirmed => confirmed),
      switchMap(() => {
        userScrobbleProvider.authenticationToken = '';

        return this.scrobbleService.saveUserScrobbleProvider(userScrobbleProvider);
      }),
    ).subscribe();
  }

  protected connectScrobbleProvider(provider: ScrobbleProvider) {
    const userScrobbleProvider = this.userScrobbleProviders().get(provider);
    if (!userScrobbleProvider) return;

    const modal = this.modalService.open(ManageUserScrobbleProviderModalComponent, {
      centered: true, fullscreen: "sm"
    });
    modal.setInput('userScrobbleProvider', userScrobbleProvider);
  }

  protected readonly ProviderSupportedEvents = ProviderSupportedEvents;
  protected readonly ScrobbleEventType = ScrobbleEventType;
  protected readonly ReviewScrobbleTargets = ReviewScrobbleTargets;
  protected readonly AgeRatings = AgeRatings;
  protected readonly ScrobbleReadStatuses = ScrobbleReadStatuses;
}
