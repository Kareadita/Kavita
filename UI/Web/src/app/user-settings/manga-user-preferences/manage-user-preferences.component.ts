import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {Preferences} from "../../_models/preferences/preferences";
import {AccountService} from "../../_services/account.service";
import {LocalizationService} from "../../_services/localization.service";
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators
} from "@angular/forms";
import {KavitaLocale} from "../../_models/metadata/language";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged, filter, forkJoin, switchMap} from "rxjs";
import {DecimalPipe, TitleCasePipe} from "@angular/common";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {LicenseService} from "../../_services/license.service";
import {HighlightBarComponent} from "../../book-reader/_components/_annotations/highlight-bar/highlight-bar.component";
import {SiteTheme} from "../../_models/preferences/site-theme";
import {PageLayoutMode} from "../../_models/page-layout-mode";
import {HighlightSlot} from "../../book-reader/_models/annotations/highlight-slot";
import {AgeRating} from "../../_models/metadata/age-rating";
import {LibraryService} from "../../_services/library.service";
import {Library} from "../../_models/library/library";
import {MetadataService} from "../../_services/metadata.service";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {TypeaheadComponent} from "../../typeahead/_components/typeahead.component";
import {TypeaheadSettings} from "../../typeahead/_models/typeahead-settings";
import {TypeaheadSettingsFactoryService} from "../../typeahead-settings-factory.service";
import {FormFieldDirective} from "../../_directives/form-field.directive";

type UserPreferencesForm = FormGroup<{
  theme: FormControl<SiteTheme>,
  globalPageLayoutMode: FormControl<PageLayoutMode>,
  blurUnreadSummaries: FormControl<boolean>,
  promptForDownloadSize: FormControl<boolean>,
  noTransitions: FormControl<boolean>,
  collapseSeriesRelationships: FormControl<boolean>,
  locale: FormControl<string>,
  bookReaderHighlightSlots: FormArray<FormControl<HighlightSlot>>,
  colorScapeEnabled: FormControl<boolean>,
  dataSaver: FormControl<boolean>,
  promptForRereadsAfter: FormControl<number>,

  aniListScrobblingEnabled: FormControl<boolean>,
  wantToReadSync: FormControl<boolean>,

  socialPreferences: FormGroup<{
    shareReviews: FormControl<boolean>,
    shareAnnotations: FormControl<boolean>,
    viewOtherAnnotations: FormControl<boolean>,
    socialLibraries: FormControl<number[]>,
    socialMaxAgeRating: FormControl<AgeRating>,
    socialIncludeUnknowns: FormControl<boolean>,
    shareProfile: FormControl<boolean>,
  }>,

  opdsPreferences: FormGroup<{
    embedProgressIndicator: FormControl<boolean>,
    includeContinueFrom: FormControl<boolean>,
  }>
}>

@Component({
  selector: 'app-manga-user-preferences',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    TitleCasePipe,
    SettingItemComponent,
    SettingSwitchComponent,
    DecimalPipe,
    HighlightBarComponent,
    AgeRatingPipe,
    TypeaheadComponent, FormFieldDirective],
  templateUrl: './manage-user-preferences.component.html',
  styleUrl: './manage-user-preferences.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageUserPreferencesComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly accountService = inject(AccountService);
  private readonly localizationService = inject(LocalizationService);
  protected readonly licenseService = inject(LicenseService);
  private readonly libraryService = inject(LibraryService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly metadataService = inject(MetadataService);
  private readonly typeaheadSettingFactory = inject(TypeaheadSettingsFactoryService);

  protected readonly isReadOnly = this.accountService.hasReadOnlyRole;
  loading = signal(true);
  ageRatings = signal<AgeRatingDto[]>([]);
  locales = signal<KavitaLocale[]>([]);
  socialLibrariesTypeaheadSettings = signal<TypeaheadSettings<Library> | null>(null);

  settingsForm!: UserPreferencesForm;


  get Locale() {
    if (!this.settingsForm.get('locale')) return 'English';

    const locale = (this.locales() || []).find(l => l.fileName === this.settingsForm.get('locale')!.value);
    if (!locale) {
      return 'English';
    }

    return locale.renderName;
  }


  constructor() {
    this.localizationService.getLocales().subscribe(res => {
      this.locales.set(res.sort((l1, l2) => l1.renderName.localeCompare(l2.renderName)));
    });
  }

  ngOnInit(): void {
    forkJoin({
      pref: this.accountService.getPreferences(),
      libraries: this.libraryService.getLibraries(),
      ageRatings: this.metadataService.getAllAgeRatings(),
    }).subscribe(({pref, libraries, ageRatings}) => {
      this.loading.set(false);
      this.ageRatings.set([{
        value: AgeRating.NotApplicable,
        title: '',
      }, ...ageRatings]);

      this.socialLibrariesTypeaheadSettings.set(this.typeaheadSettingFactory.forLibraries({id: 'social-libraries', libraries}));

      this.settingsForm = this.fb.group({
        theme: this.fb.control<SiteTheme>(pref.theme),
        globalPageLayoutMode: this.fb.control<PageLayoutMode>(pref.globalPageLayoutMode),
        blurUnreadSummaries: this.fb.control<boolean>(pref.blurUnreadSummaries),
        promptForDownloadSize: this.fb.control<boolean>(pref.promptForDownloadSize),
        noTransitions: this.fb.control<boolean>(pref.noTransitions),
        collapseSeriesRelationships: this.fb.control<boolean>(pref.collapseSeriesRelationships),
        locale: this.fb.control<string>(pref.locale || 'en'),
        bookReaderHighlightSlots: this.fb.array(pref.bookReaderHighlightSlots.map(s => this.fb.control(s))),
        colorScapeEnabled: this.fb.control<boolean>(pref.colorScapeEnabled),
        dataSaver: this.fb.control<boolean>(pref.dataSaver),
        promptForRereadsAfter: this.fb.control<number>(pref.promptForRereadsAfter, [Validators.required]), // Required allows 0, but not null

        aniListScrobblingEnabled: this.fb.control<boolean>(pref.aniListScrobblingEnabled),
        wantToReadSync: this.fb.control<boolean>(pref.wantToReadSync),

        socialPreferences: this.fb.group({
          shareReviews: this.fb.control<boolean>(pref.socialPreferences.shareReviews),
          shareAnnotations: this.fb.control<boolean>(pref.socialPreferences.shareAnnotations),
          viewOtherAnnotations: this.fb.control<boolean>(pref.socialPreferences.viewOtherAnnotations),
          socialLibraries: this.fb.control<number[]>(pref.socialPreferences.socialLibraries),
          socialMaxAgeRating: this.fb.control<AgeRating>(pref.socialPreferences.socialMaxAgeRating),
          socialIncludeUnknowns: this.fb.control<boolean>(pref.socialPreferences.socialIncludeUnknowns),
          shareProfile: this.fb.control<boolean>(pref.socialPreferences.shareProfile),
        }),

        opdsPreferences: this.fb.group({
          embedProgressIndicator: this.fb.control<boolean>(pref.opdsPreferences.embedProgressIndicator),
          includeContinueFrom: this.fb.control<boolean>(pref.opdsPreferences.includeContinueFrom),
        })
      });

      if (this.isReadOnly()) {
        this.settingsForm.disable({ emitEvent: false });
      }

      this.settingsForm.markAsPristine();

      // Automatically save settings as we edit them
      this.settingsForm.valueChanges.pipe(
        distinctUntilChanged(),
        debounceTime(100),
        filter(_ => this.settingsForm.valid && this.settingsForm.dirty),
        takeUntilDestroyed(this.destroyRef),
        switchMap(_ => {
          const data = this.packSettings();
          return this.accountService.updatePreferences(data);
        }),
      ).subscribe();
    });
  }

  syncFormWithTypeahead(libs: Library[] | Library) {
    this.settingsForm
      .get('socialPreferences')!
      .get('socialLibraries')!
      .setValue((libs as Library[]).map(l => l.id));
  }

  packSettings(): Preferences {
    const customKeyBinds = this.accountService.userPreferences()!.customKeyBinds;
    return {
      customKeyBinds,
      ...this.settingsForm.getRawValue(),
    };
  }
}
