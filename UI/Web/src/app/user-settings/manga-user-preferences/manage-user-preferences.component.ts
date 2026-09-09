import {
  ChangeDetectionStrategy,
  Component,
  computed, debounced,
  DestroyRef,
  effect,
  inject,
  OnInit,
  Signal,
  signal
} from '@angular/core';
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
import {SiteTheme, ThemeProvider} from "../../_models/preferences/site-theme";
import {PageLayoutMode} from "../../_models/page-layout-mode";
import {HighlightSlot} from "../../book-reader/_models/annotations/highlight-slot";
import {AgeRating} from "../../_models/metadata/age-rating";
import {LibraryService} from "../../_services/library.service";
import {Library} from "../../_models/library/library";
import {MetadataService} from "../../_services/metadata.service";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {AgeRatingPipe} from "../../_pipes/age-rating.pipe";
import {TypeaheadComponent} from "../../typeahead/_components/typeahead.component";
import {TypeaheadConfig} from "../../typeahead/_models/typeahead-config";
import {TypeaheadConfigFactoryService} from "../../typeahead-config-factory.service";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {debounce, disabled, form, FormField, min, required} from "@angular/forms/signals";
import {SettingEnumSelectComponent} from "../../settings/_components/setting-enum-select/setting-enum-select.component";

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
    TypeaheadComponent,
    FormFieldDirective,
    FormField,
    SettingEnumSelectComponent
  ],
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
  private readonly typeaheadSettingFactory = inject(TypeaheadConfigFactoryService);

  protected readonly isReadOnly = this.accountService.hasReadOnlyRole;
  loading = signal(true);
  ageRatings = signal<AgeRatingDto[]>([]);
  locales = signal<KavitaLocale[]>([]);
  socialLibrariesTypeaheadSettings = signal<TypeaheadConfig<Library> | null>(null);

  userPreferencesModel = signal<Preferences>({
    aniListScrobblingEnabled: false,
    blurUnreadSummaries: false,
    bookReaderHighlightSlots: [],
    collapseSeriesRelationships: false,
    colorScapeEnabled: false,
    customKeyBinds: {},
    dataSaver: false,
    globalPageLayoutMode: PageLayoutMode.Cards,
    locale: "",
    noTransitions: false,
    opdsPreferences: {
      embedProgressIndicator: false,
      includeContinueFrom: false
    },
    promptForDownloadSize: false,
    promptForRereadsAfter: 0,
    socialPreferences: {
      shareReviews: false,
      shareAnnotations: false,
      viewOtherAnnotations: false,
      socialLibraries: [],
      socialMaxAgeRating: AgeRating.NotApplicable,
      socialIncludeUnknowns: false,
      shareProfile: false
    },
    theme: {
      id: 0,
      name: "",
      normalizedName: "",
      filePath: "",
      isDefault: false,
      provider: ThemeProvider.System,
      selector: "",
      description: "",
      previewUrls: [],
      author: ""
    },
    wantToReadSync: false
  });
  userPreferencesForm = form(this.userPreferencesModel, (path) => {
    disabled(path, {when: () => this.accountService.hasReadOnlyRole()});
    debounce(path, 100);

    min(path.promptForRereadsAfter, 0);
    required(path.promptForRereadsAfter);
  });

  selectedLocale = computed(() => {
    const locale = (this.locales() || []).find(l => l.fileName === this.userPreferencesForm.locale().value());
    if (!locale) {
      return 'English';
    }

    return locale.renderName;
  });


  constructor() {
    this.localizationService.getLocales().subscribe(res => {
      this.locales.set(res.sort((l1, l2) => l1.renderName.localeCompare(l2.renderName)));
    });

    effect(() => {
      if (!this.userPreferencesForm().valid() || !this.userPreferencesForm().dirty || this.loading()) {
        return;
      }
      const preferences = this.userPreferencesModel();

      this.accountService.updatePreferences(preferences).subscribe();
    });
  }

  ngOnInit(): void {
    forkJoin({
      pref: this.accountService.getPreferences(),
      libraries: this.libraryService.getLibraries(),
      ageRatings: this.metadataService.getAllAgeRatings(),
    }).subscribe(({pref, libraries, ageRatings}) => {
      this.ageRatings.set([{value: AgeRating.NotApplicable, title: '',}, ...ageRatings]);
      this.socialLibrariesTypeaheadSettings.set(this.typeaheadSettingFactory.forLibraries({id: 'social-libraries', libraries}));
      this.userPreferencesModel.set(pref);

      this.loading.set(false);
    });
  }

  syncFormWithTypeahead(libs: Library[] | Library) {
    this.userPreferencesForm.socialPreferences.socialLibraries().value.set((libs as Library[]).map(l => l.id));
  }
}
