import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {Preferences} from "../../_models/preferences/preferences";
import {AccountService} from "../../_services/account.service";
import {BookService} from "../../book-reader/_services/book.service";
import {Title} from "@angular/platform-browser";
import {Router} from "@angular/router";
import {LocalizationService} from "../../_services/localization.service";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
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
    HighlightBarComponent
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


  fontFamilies: Array<string> = [];
  locales: Array<KavitaLocale> = [];

  settingsForm: FormGroup = new FormGroup({});
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
    this.fontFamilies = this.bookService.getFontFamilies().map(f => f.title);
    this.cdRef.markForCheck();

    this.localizationService.getLocales().subscribe(res => {
      this.locales = res;

      this.cdRef.markForCheck();
    });
  }

  ngOnInit(): void {
    this.titleService.setTitle('Kavita - User Preferences');

    forkJoin({
      user: this.accountService.currentUser$.pipe(take(1)),
      pref: this.accountService.getPreferences()
    }).subscribe(results => {
      if (results.user === undefined) {
        this.router.navigateByUrl('/login');
        return;
      }

      this.user = results.user;
      this.user.preferences = results.pref;

      this.settingsForm.addControl('theme', new FormControl(this.user.preferences.theme, []));
      this.settingsForm.addControl('globalPageLayoutMode', new FormControl(this.user.preferences.globalPageLayoutMode, []));
      this.settingsForm.addControl('blurUnreadSummaries', new FormControl(this.user.preferences.blurUnreadSummaries, []));
      this.settingsForm.addControl('promptForDownloadSize', new FormControl(this.user.preferences.promptForDownloadSize, []));
      this.settingsForm.addControl('noTransitions', new FormControl(this.user.preferences.noTransitions, []));
      this.settingsForm.addControl('collapseSeriesRelationships', new FormControl(this.user.preferences.collapseSeriesRelationships, []));
      this.settingsForm.addControl('shareReviews', new FormControl(this.user.preferences.shareReviews, []));
      this.settingsForm.addControl('locale', new FormControl(this.user.preferences.locale || 'en', []));
      this.settingsForm.addControl('colorScapeEnabled', new FormControl(this.user.preferences.colorScapeEnabled ?? true, []));

      this.settingsForm.addControl('aniListScrobblingEnabled', new FormControl(this.user.preferences.aniListScrobblingEnabled || false, []));
      this.settingsForm.addControl('wantToReadSync', new FormControl(this.user.preferences.wantToReadSync || false, []));
      this.settingsForm.addControl('bookReaderHighlightSlots', new FormControl(this.user.preferences.bookReaderHighlightSlots, []));


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

    this.settingsForm.get('bookReaderImmersiveMode')?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(mode => {
      if (mode) {
        this.settingsForm.get('bookReaderTapToPaginate')?.setValue(true);
        this.cdRef.markForCheck();
      }
    });
    this.cdRef.markForCheck();
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
    const modelSettings = this.settingsForm.value;

    return  {
      theme: modelSettings.theme,
      globalPageLayoutMode: parseInt(modelSettings.globalPageLayoutMode, 10),
      blurUnreadSummaries: modelSettings.blurUnreadSummaries,
      promptForDownloadSize: modelSettings.promptForDownloadSize,
      noTransitions: modelSettings.noTransitions,
      collapseSeriesRelationships: modelSettings.collapseSeriesRelationships,
      shareReviews: modelSettings.shareReviews,
      locale: modelSettings.locale || 'en',
      aniListScrobblingEnabled: modelSettings.aniListScrobblingEnabled,
      wantToReadSync: modelSettings.wantToReadSync,
      bookReaderHighlightSlots: modelSettings.bookReaderHighlightSlots,
      colorScapeEnabled: modelSettings.colorScapeEnabled,
    };
  }
}
