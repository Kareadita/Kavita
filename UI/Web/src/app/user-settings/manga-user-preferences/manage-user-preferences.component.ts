import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {Preferences} from "../../_models/preferences/preferences";
import {AccountService} from "../../_services/account.service";
import {Title} from "@angular/platform-browser";
import {Router} from "@angular/router";
import {LocalizationService} from "../../_services/localization.service";
import {FormArray, FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule} from "@angular/forms";
import {User} from "../../_models/user/user";
import {KavitaLocale} from "../../_models/metadata/language";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged, filter, forkJoin, of, switchMap, tap} from "rxjs";
import {take} from "rxjs/operators";
import {AsyncPipe, DecimalPipe, TitleCasePipe} from "@angular/common";
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
    AsyncPipe,
    DecimalPipe,
    HighlightBarComponent,
    AgeRatingPipe,
    TypeaheadComponent,
  ],
  templateUrl: './manage-user-preferences.component.html',
  styleUrl: './manage-user-preferences.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageUserPreferencesComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly accountService = inject(AccountService);
  private readonly titleService = inject(Title);
  private readonly router = inject(Router);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly localizationService = inject(LocalizationService);
  protected readonly licenseService = inject(LicenseService);
  private readonly libraryService = inject(LibraryService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly metadataService = inject(MetadataService);


  loading = signal(true);
  ageRatings = signal<AgeRatingDto[]>([]);
  libraries = signal<Library[]>([]);

  locales: Array<KavitaLocale> = [];

  settingsForm!: UserPreferencesForm;
  user: User | undefined = undefined;
  libraryTypeAheadSettings = signal(new TypeaheadSettings<Library>());

  get Locale() {
    if (!this.settingsForm.get('locale')) return 'English';

    const locale = (this.locales || []).find(l => l.fileName === this.settingsForm.get('locale')!.value);
    if (!locale) {
      return 'English';
    }

    return locale.renderName;
  }


  constructor() {
    this.localizationService.getLocales().subscribe(res => {
      this.locales = res.sort((l1, l2) => {
        return l1.renderName.localeCompare(l2.renderName)
      });

      this.cdRef.markForCheck();
    });
  }

  ngOnInit(): void {
    this.titleService.setTitle('Kavita - User Preferences');
    this.cdRef.markForCheck();

    forkJoin({
      user: this.accountService.currentUser$.pipe(take(1)),
      pref: this.accountService.getPreferences(),
      libraries: this.libraryService.getLibraries(),
      ageRatings: this.metadataService.getAllAgeRatings(),
    }).subscribe(({user, pref, libraries, ageRatings}) => {
      if (user === undefined) {
        this.router.navigateByUrl('/login');
        return;
      }

      this.user = user;
      this.user.preferences = pref;

      this.loading.set(false);
      this.libraries.set(libraries);
      this.ageRatings.set([{
        value: AgeRating.NotApplicable,
        title: '',
      }, ...ageRatings]);

      this.setupLibraryTypeAheadSettings();

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
        promptForRereadsAfter: this.fb.control<number>(pref.promptForRereadsAfter),

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

      // Automatically save settings as we edit them
      this.settingsForm.valueChanges.pipe(
        distinctUntilChanged(),
        debounceTime(100),
        filter(_ => this.settingsForm.valid),
        takeUntilDestroyed(this.destroyRef),
        switchMap(_ => {
          const data = this.packSettings();
          return this.accountService.updatePreferences(data);
        }),
        tap(prefs => {
          if (this.user) {
            this.user.preferences = {...prefs};
            this.cdRef.markForCheck();
          }
        })
      ).subscribe();

      this.cdRef.markForCheck();
    });
  }

  private setupLibraryTypeAheadSettings() {
    const libs = this.libraries();
    const selectedLibs = this.user!.preferences.socialPreferences.socialLibraries;

    const settings = new TypeaheadSettings<Library>();
    settings.multiple = true;
    settings.minCharacters = 0;
    settings.savedData = libs.filter(l => selectedLibs.includes(l.id));
    settings.compareFn = (libs, filter) => libs.filter(l => l.name.toLowerCase().includes(filter.toLowerCase()));
    settings.trackByIdentityFn = (idx, l) => `${l.id}`;
    settings.fetchFn = (filter) => of(settings.compareFn(libs, filter));

    this.libraryTypeAheadSettings.set(settings);
  }

  syncFormWithTypeahead(libs: Library[] | Library) {
    this.settingsForm
      .get('socialPreferences')!
      .get('socialLibraries')!
      .setValue((libs as Library[]).map(l => l.id));
  }

  packSettings(): Preferences {
    const customKeyBinds = this.accountService.currentUserSignal()!.preferences.customKeyBinds;
    return {
      customKeyBinds,
      ...this.settingsForm.getRawValue(),
    };
  }
}
