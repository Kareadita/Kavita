import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {filter, forkJoin, map, of, switchMap, tap} from "rxjs";
import {AgeRatings} from "../../_models/metadata/age-rating";
import {ReviewScrobbleTargets} from "../../_models/kavitaplus/scrobble-providers/review-scrobble-target.enum";
import {ScrobbleProviderSettings} from "../../_models/kavitaplus/scrobble-providers/scrobble-provider-settings";
import {ScrobbleReadStatuses} from "../../_models/kavitaplus/scrobble-providers/scrobble-read-status.enum";
import {UserScrobbleProvider} from "../../_models/kavitaplus/scrobble-providers/user-scrobble-provider";
import {PublicationStatuses} from "../../_models/metadata/publication-status";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {catchError, debounceTime, take} from "rxjs/operators";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProviderNamePipe} from "../../_pipes/scrobble-provider-name.pipe";
import {ScrobbleEventType} from "../../_models/scrobbling/scrobble-event";
import {ReviewScrobbleTargetNamePipe} from "../../_pipes/review-scrobble-target-name.pipe";
import {Library, LibraryType} from "../../_models/library/library";
import {LibraryService} from "../../_services/library.service";
import {PublicationStatusPipe} from "../../_pipes/publication-status.pipe";
import {ScrobbleReadStatusPipe} from "../../_pipes/scrobble-read-status.pipe";
import {Select2, Select2Data} from "ng-select2-component";
import {TypeaheadConfig} from "../../typeahead/_models/typeahead-config";
import {ModalService} from "../../_services/modal.service";
import {
  ManageUserScrobbleProviderModalComponent
} from "../_modals/manage-user-scrobble-provider-modal/manage-user-scrobble-provider-modal.component";
import {ConfirmService} from "../../shared/confirm.service";
import {fromPromise} from "rxjs/internal/observable/innerFrom";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {ScrobbleProviderUpdatedEvent} from "../../_models/events/scrobble-provider-updated-event";
import {NgOptimizedImage} from "@angular/common";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ToastrService} from '@openng/ngx-toastr';
import {AccordionComponent} from "../../shared/accordion/accordion.component";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";
import {ScrobbleProviderDescriptionPipe} from "../../_pipes/scrobble-provider-description.pipe";
import {TagBadgeComponent} from "../../shared/tag-badge/tag-badge.component";
import {UtcToLocalDatePipe} from "../../_pipes/utc-to-locale-date.pipe";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {TimeDifferencePipe} from "../../_pipes/time-difference.pipe";
import {TypeaheadComponent} from "../../typeahead/_components/typeahead.component";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {ActivatedRoute} from "@angular/router";
import {TypeaheadConfigFactoryService} from "../../typeahead-config-factory.service";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {applyEach, disabled, FieldTree, form, FormField} from "@angular/forms/signals";
import {SettingSelectComponent} from "../../settings/_components/setting-enum-select/setting-select.component";

interface ProviderFormEntry {
  provider: ScrobbleProvider;
  settings: ScrobbleProviderSettings;
}

interface FormModel {
  providers: ProviderFormEntry[];
}

const ProviderSupportedEvents: Record<ScrobbleProvider, ScrobbleEventType[]> = {
  [ScrobbleProvider.AniList]: [ScrobbleEventType.ScoreUpdated, ScrobbleEventType.Review, ScrobbleEventType.ChapterRead, ScrobbleEventType.AddWantToRead],
  [ScrobbleProvider.Hardcover]: [ScrobbleEventType.ScoreUpdated, ScrobbleEventType.Review, ScrobbleEventType.ChapterRead, ScrobbleEventType.AddWantToRead],
  [ScrobbleProvider.Mal]: [ScrobbleEventType.AddWantToRead, ScrobbleEventType.ScoreUpdated, ScrobbleEventType.ChapterRead],
  [ScrobbleProvider.MangaBaka]: [ScrobbleEventType.ScoreUpdated, ScrobbleEventType.Review, ScrobbleEventType.ChapterRead, ScrobbleEventType.AddWantToRead],
  [ScrobbleProvider.Cbr]: [],
  [ScrobbleProvider.Kavita]: []
}

const ProvidersSupportLibraryTypes: Record<ScrobbleProvider, LibraryType[]> = {
  [ScrobbleProvider.AniList]: [LibraryType.Manga, LibraryType.LightNovel],
  [ScrobbleProvider.Hardcover]: [LibraryType.LightNovel, LibraryType.Book, LibraryType.Comic, LibraryType.ComicVine],
  [ScrobbleProvider.Mal]: [LibraryType.Manga, LibraryType.LightNovel],
  [ScrobbleProvider.MangaBaka]: [LibraryType.Manga, LibraryType.LightNovel],
  [ScrobbleProvider.Cbr]: [LibraryType.Comic],
  [ScrobbleProvider.Kavita]: []
}

@Component({
  selector: 'app-manage-scrobble-providers',
  imports: [
    TranslocoDirective,
    ReviewScrobbleTargetNamePipe,
    ScrobbleReadStatusPipe,
    Select2,
    NgOptimizedImage,
    NgbTooltip,
    AccordionComponent,
    LoadingComponent,
    ProviderImagePipe,
    ScrobbleProviderNamePipe,
    ScrobbleProviderDescriptionPipe,
    TagBadgeComponent,
    UtcToLocalDatePipe,
    DefaultValuePipe,
    UtcToLocalTimePipe,
    TimeDifferencePipe,
    TypeaheadComponent,
    AgeRatingPipe, FormFieldDirective, FormField, SettingSelectComponent],
  templateUrl: './manage-scrobble-providers.component.html',
  styleUrl: './manage-scrobble-providers.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManageScrobbleProvidersComponent implements OnInit {

  protected readonly scrobbleService = inject(ScrobblingService);
  private readonly libraryService = inject(LibraryService);
  private readonly destroyRef$ = inject(DestroyRef);
  private readonly modalService = inject(ModalService);
  private readonly confirmService = inject(ConfirmService);
  private readonly messageHub = inject(MessageHubService);
  private readonly scrobblingService = inject(ScrobblingService);
  private readonly toastr = inject(ToastrService);
  private readonly route = inject(ActivatedRoute);
  private readonly typeaheadSettingsFactory = inject(TypeaheadConfigFactoryService);

  userScrobbleProviders = signal<Map<ScrobbleProvider, UserScrobbleProvider>>(new Map());
  loading = computed(() => this.userScrobbleProviders().size === 0);

  isLoadingInBackground = signal(false);
  libraries = signal<Library[]>([]);
  backfillAttempts: Map<ScrobbleProvider, number> = new Map();

  private readonly formModel = signal<FormModel>({providers: []});

  protected readonly formGroup = form(this.formModel, p => {
    applyEach(p.providers, entry => {
      disabled(entry.settings.reviewScrobbleTarget, ({valueOf}) => !valueOf(entry.settings.reviewsScrobbling));
    });
  });

  private readonly providerIndexes = computed(() =>
    new Map(this.formGroup.providers().value().map((e, i) => [e.provider, i])));

  /** Last settings sent to the server, so the autosave only fires for providers the user actually changed */
  private readonly savedSettings = new Map<ScrobbleProvider, string>();

  private readonly publicationStatusPipe = new PublicationStatusPipe();
  private readonly scrobbleProviderNamePipe = new ScrobbleProviderNamePipe();

  publicationStatusOptions: Select2Data = PublicationStatuses.map(p => ({
    value: p,
    label: this.publicationStatusPipe.transform(p)
  }));

  constructor() {
    toObservable(this.formModel).pipe(
      takeUntilDestroyed(this.destroyRef$),
      debounceTime(500),
      switchMap(model => {
        const changed = model.providers.filter(e => this.savedSettings.get(e.provider) !== JSON.stringify(e.settings));
        if (changed.length === 0) return of(null);

        changed.forEach(e => this.savedSettings.set(e.provider, JSON.stringify(e.settings)));

        return forkJoin(changed.map(e => this.scrobbleService.saveScrobbleSettings(e.provider, e.settings))).pipe(
          catchError(err => {
            console.error(err);
            this.toastr.error(translate('errors.generic'));
            return of(null);
          })
        );
      })
    ).subscribe();
  }

  ngOnInit() {
    this.libraryService.getLibraries().subscribe(libraries => this.libraries.set(libraries));

    this.route.queryParamMap.pipe(
      map(m => m.get('loading')),
      take(1),
      filter(loading => loading === 'true'),
      tap(() => this.isLoadingInBackground.set(true))
    ).subscribe();

    this.loadData().subscribe();

    this.messageHub.messages$.pipe(
      takeUntilDestroyed(this.destroyRef$),
      filter(msg => msg.event === EVENTS.ScrobbleProviderUpdated),
      map(msg => (msg.payload as ScrobbleProviderUpdatedEvent).provider),
      switchMap(() => this.loadData()),
      tap(() => this.isLoadingInBackground.set(false))
    ).subscribe();
  }

  private loadData() {
    return this.scrobbleService.getScrobbleProviders()
      .pipe(tap(userScrobbleProviders => {
        for (const p of userScrobbleProviders) {
          this.savedSettings.set(p.provider, JSON.stringify(p.settings));

          // Build up backfill attempt map (we only keep track of if it ran, it's only important to tell the user it was run)
          this.backfillAttempts.set(p.provider, p.hasRunScrobbleEventGeneration ? 1 : 0);
        }

        this.userScrobbleProviders.set(new Map(userScrobbleProviders.map(p => [p.provider, p])));
        this.formModel.set({
          providers: userScrobbleProviders.map(p => ({provider: p.provider, settings: p.settings}))
        });
      }));
  }

  protected providerField(provider: ScrobbleProvider): FieldTree<ProviderFormEntry> | undefined {
    const index = this.providerIndexes().get(provider);
    if (index === undefined) return undefined;

    return this.formGroup.providers[index];
  }

  protected libraryTypeaheadSettings(provider: ScrobbleProvider): TypeaheadConfig<Library> {
    const libraries = this.libraries()
      .filter(l => ProvidersSupportLibraryTypes[provider].includes(l.type));

    const userScrobbleProvider = this.userScrobbleProviders().get(provider)!;

    return this.typeaheadSettingsFactory.forLibraries({id: `libraries-${provider}`, libraries, overrides: {
        savedData: libraries.filter(l => userScrobbleProvider.settings.libraries.includes(l.id))
      }
    });
  }

  updateLibrarySelection(provider: ScrobbleProvider, libraries: Library[]) {
    this.providerField(provider)?.settings.libraries().value.set(libraries.map(l => l.id));
  }

  protected async disconnectScrobbleProvider(provider: ScrobbleProvider) {
    fromPromise(this.confirmService.confirm(translate('scrobble-provider-settings-manager.confirm-delete',
      {provider: this.scrobbleProviderNamePipe.transform(provider)}))).pipe(
      filter(confirmed => confirmed),
      switchMap(() => {
        return this.scrobbleService.saveUserScrobbleProvider({
          provider: provider,
          authenticationToken: '',
          userName: '',
        });
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

  protected async backfillEvents(provider: ScrobbleProvider) {
    if (this.backfillAttempts.has(provider) && this.backfillAttempts.get(provider)! > 0) {
      // Alert the user they have already run this X times before
      if (!await this.confirmService.confirm(translate('toasts.confirm-rerun-backfill', {provider: this.scrobbleProviderNamePipe.transform(provider)}))) return;
    }

    this.scrobblingService.triggerScrobbleEventGeneration(provider).subscribe(_ => {
      this.backfillAttempts.set(provider, (this.backfillAttempts.get(provider) ?? 0) + 1);
      this.toastr.info(translate('toasts.scrobble-gen-init'));
    });
  }

  protected readonly ProviderSupportedEvents = ProviderSupportedEvents;
  protected readonly ScrobbleEventType = ScrobbleEventType;
  protected readonly ReviewScrobbleTargets = ReviewScrobbleTargets;
  protected readonly AgeRatings = AgeRatings;
  protected readonly ScrobbleReadStatuses = ScrobbleReadStatuses;
  protected readonly ScrobbleProvider = ScrobbleProvider;
}
