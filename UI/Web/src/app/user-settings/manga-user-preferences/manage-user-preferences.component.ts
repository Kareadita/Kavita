import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal
} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {Preferences} from "../../_models/preferences/preferences";
import {AccountService} from "../../_services/account.service";
import {BookService} from "../../book-reader/_services/book.service";
import {Title} from "@angular/platform-browser";
import {Router} from "@angular/router";
import {LocalizationService} from "../../_services/localization.service";
import {Form, FormArray, FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule} from "@angular/forms";
import {User} from "../../_models/user";
import {KavitaLocale} from "../../_models/metadata/language";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged, filter, forkJoin, switchMap, tap} from "rxjs";
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
import {MultiCheckBoxFormComponent} from "../../shared/_components/multi-check-box-form/multi-check-box-form.component";
import {MetadataService} from "../../_services/metadata.service";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";

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

  aniListScrobblingEnabled: FormControl<boolean>,
  wantToReadSync: FormControl<boolean>,

  shareReviews: FormControl<boolean>,
  shareAnnotations: FormControl<boolean>,
  viewOtherAnnotations: FormControl<boolean>,
  socialLibraries: FormControl<number[]>,
  socialMaxAgeRating: FormControl<AgeRating>,
  socialIncludeUnknowns: FormControl<boolean>,
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
    MultiCheckBoxFormComponent,
    AgeRatingPipe,
  ],
  templateUrl: './manage-user-preferences.component.html',
  styleUrl: './manage-user-preferences.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageUserPreferencesComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly accountService = inject(AccountService);
  private readonly bookService = inject(BookService);
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
  libraryOptions = computed(() => this.libraries().map(l => {
    return { label: l.name, value: l.id };
  }));

  locales: Array<KavitaLocale> = [];

  settingsForm!: UserPreferencesForm;
  user: User | undefined = undefined;

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

      this.loading.set(false);
      this.libraries.set(libraries);
      this.ageRatings.set(ageRatings);
      this.user = user;
      this.user.preferences = pref;

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

        aniListScrobblingEnabled: this.fb.control<boolean>(pref.aniListScrobblingEnabled),
        wantToReadSync: this.fb.control<boolean>(pref.wantToReadSync),

        shareReviews: this.fb.control<boolean>(pref.shareReviews),
        shareAnnotations: this.fb.control<boolean>(pref.shareAnnotations),
        viewOtherAnnotations: this.fb.control<boolean>(pref.viewOtherAnnotations),
        socialLibraries: this.fb.control<number[]>(pref.socialLibraries),
        socialMaxAgeRating: this.fb.control<AgeRating>(pref.socialMaxAgeRating),
        socialIncludeUnknowns: this.fb.control<boolean>(pref.socialIncludeUnknowns),
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

  reset() {
    if (!this.user) return;

    this.settingsForm.get('theme')?.setValue(this.user.preferences.theme, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('globalPageLayoutMode')?.setValue(this.user.preferences.globalPageLayoutMode, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('blurUnreadSummaries')?.setValue(this.user.preferences.blurUnreadSummaries, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('promptForDownloadSize')?.setValue(this.user.preferences.promptForDownloadSize, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('noTransitions')?.setValue(this.user.preferences.noTransitions, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('collapseSeriesRelationships')?.setValue(this.user.preferences.collapseSeriesRelationships, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('shareReviews')?.setValue(this.user.preferences.shareReviews, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('locale')?.setValue(this.user.preferences.locale || 'en', {onlySelf: true, emitEvent: false});
    this.settingsForm.get('colorScapeEnabled')?.setValue(this.user.preferences.colorScapeEnabled ?? true, {onlySelf: true, emitEvent: false});

    this.settingsForm.get('aniListScrobblingEnabled')?.setValue(this.user.preferences.aniListScrobblingEnabled || false, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('wantToReadSync')?.setValue(this.user.preferences.wantToReadSync || false, {onlySelf: true, emitEvent: false});
    this.settingsForm.get('bookReaderHighlightSlots')?.setValue(this.user.preferences.bookReaderHighlightSlots, {onlySelf: true, emitEvent: false});
  }

  packSettings(): Preferences {
    return this.settingsForm.getRawValue();
  }
}
