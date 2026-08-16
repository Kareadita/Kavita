import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
  viewChild
} from '@angular/core';
import {NgTemplateOutlet} from "@angular/common";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {AbstractControl, FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {SettingsService} from "../settings.service";
import {debounceTime, filter, switchMap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {map, tap} from "rxjs/operators";
import {MetadataSettings, SeriesNameLanguage} from "../_models/metadata-settings";
import {PersonRole} from "../../_models/metadata/person";
import {PersonRolePipe} from "../../_pipes/person-role.pipe";
import {allMetadataSettingField, MetadataSettingField} from "../_models/metadata-setting-field";
import {MetadataSettingFiledPipe} from "../../_pipes/metadata-setting-filed.pipe";
import {
  ManageMetadataMappingsComponent,
  MetadataMappingsExport
} from "../manage-metadata-mappings/manage-metadata-mappings.component";
import {RouterLink} from "@angular/router";
import {SettingsTabId} from "../../sidenav/preference-nav/preference-nav.component";
import {ModalService} from "../../_services/modal.service";
import {
  RunMetadataMappingsModalComponent
} from "../manage-metadata-mappings/run-metadata-mappings-modal/run-metadata-mappings-modal.component";
import {DefaultModalOptions} from "../../_models/modal/modal-options";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {NotificationProgressEvent} from "../../_models/events/notification-progress-event";
import {QueueNames, ServerService, TaskMethodNames} from "../../_services/server.service";
import {
  NgbAccordionBody,
  NgbAccordionButton,
  NgbAccordionCollapse,
  NgbAccordionDirective,
  NgbAccordionHeader,
  NgbAccordionItem
} from "@ng-bootstrap/ng-bootstrap";
import {
  SettingMultiTextFieldComponent
} from "../../settings/_components/setting-multi-text-field/setting-multi-text-field.component";
import {MetadataService} from "../../_services/metadata.service";
import {LibraryService} from "../../_services/library.service";
import {Library} from "../../_models/library/library";
import {Language} from "../../_models/metadata/language";
import {
  isNativeToken,
  isRomajiToken,
  languageCodeListValidator,
  primarySubtag,
  splitLanguageCodes,
  unknownLanguageSubtags,
  unknownScriptSubtags
} from "../../shared/utils/language-code.util";
import {
  AgeRatingMapperComponent,
  buildAgeRatingMappingsArray,
  packAgeRatingMappings
} from "../../shared/_components/age-rating-mapper/age-rating-mapper.component";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {TagWeightTitlePipe} from "../../_pipes/tag-weight-title.pipe";
import {allTagWeights} from "../_models/tag-weight.enum";

const MangaBakaAgeRatings = ['Safe', 'Suggestive', 'Erotica', 'Pornographic'];

@Component({
  selector: 'app-manage-metadata-settings',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SettingSwitchComponent,
    PersonRolePipe,
    MetadataSettingFiledPipe,
    ManageMetadataMappingsComponent,
    RouterLink,
    NgbAccordionDirective,
    NgbAccordionItem,
    NgbAccordionHeader,
    NgbAccordionButton,
    NgbAccordionCollapse,
    NgbAccordionBody,
    SettingMultiTextFieldComponent,
    NgTemplateOutlet,
    AgeRatingMapperComponent,
    SettingItemComponent,
    TagWeightTitlePipe,

  ],
  templateUrl: './manage-metadata-settings.component.html',
  styleUrl: './manage-metadata-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMetadataSettingsComponent implements OnInit {

  readonly manageMetadataMappingsComponent = viewChild.required(ManageMetadataMappingsComponent);

  private readonly settingService = inject(SettingsService);
  private readonly metadataService = inject(MetadataService);
  private readonly libraryService = inject(LibraryService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);
  private readonly modalService = inject(ModalService);
  private readonly messageHub = inject(MessageHubService);
  private readonly serverService = inject(ServerService);

  settingsForm: FormGroup = new FormGroup({});
  settings = signal<MetadataSettings | undefined>(undefined);
  personRoles = signal<PersonRole[]>([PersonRole.Writer, PersonRole.CoverArtist, PersonRole.Character]);
  isLoaded = signal<boolean>(false);


  isReRunInProgress = signal(true);

  libraryLanguageOverrides = this.fb.array<FormGroup<{
    libraryId: FormControl<number | null>,
    name: FormControl<string | null>,
    localizedName: FormControl<string | null>,
  }>>([]);

  libraries = signal<Array<Library>>([]);
  bcp47Languages = signal<Array<Language>>([]);

  get globalLanguageGroup(): FormGroup | null {
    return this.settingsForm.get('globalLanguageTitleSettings') as FormGroup | null;
  }

  private languageCodeControls(): Array<AbstractControl> {
    const globalGroup = this.globalLanguageGroup;
    const controls: Array<AbstractControl> = [];

    if (globalGroup) {
      controls.push(globalGroup.get('name')!, globalGroup.get('localizedName')!);
    }

    for (const row of this.libraryLanguageOverrides.controls) {
      controls.push(row.get('name')!, row.get('localizedName')!);
    }

    return controls;
  }

  /**
   * Whether a malformed language code is currently blocking saves.
   */
  hasMalformedLanguageCodes(): boolean {
    return this.languageCodeControls().some(c => !!c.errors?.['malformedLanguageCodes']);
  }

  private readonly languageTitleByCode = computed(() => {
    const map = new Map<string, string>();
    for (const lang of this.bcp47Languages()) {
      map.set(lang.isoCode.toLowerCase(), lang.title);
    }
    return map;
  });

  /**
   * The set of language subtags the server recognizes, used only to warn
   */
  private readonly knownPrimarySubtags = computed(
    () => new Set(this.bcp47Languages().map(l => primarySubtag(l.isoCode)))
  );

  ngOnInit(): void {
    this.metadataService.getAllBcp47Languages().subscribe(languages => {
      this.bcp47Languages.set(languages);
    });

    this.libraryService.getLibraries().subscribe(libraries => {
      this.libraries.set(libraries);
    });

    this.settingService.getMetadataSettings().subscribe(settings => {
      this.settings.set(settings);

      this.settingsForm.addControl('enabled', new FormControl(settings.enabled, []));
      this.settingsForm.addControl('enableExtendedMetadataProcessing', new FormControl(settings.enableExtendedMetadataProcessing, []));
      this.settingsForm.addControl('enableSummary', new FormControl(settings.enableSummary, []));
      this.settingsForm.addControl('enableLocalizedName', new FormControl(settings.enableLocalizedName, []));
      this.settingsForm.addControl('enableName', new FormControl(settings.enableName, []));
      this.settingsForm.addControl('enablePublicationStatus', new FormControl(settings.enablePublicationStatus, []));
      this.settingsForm.addControl('enableAgeRating', new FormControl(settings.enableAgeRating, []));
      this.settingsForm.addControl('enableRelations', new FormControl(settings.enableRelationships, []));
      this.settingsForm.addControl('enableGenres', new FormControl(settings.enableGenres, []));
      this.settingsForm.addControl('enableTags', new FormControl(settings.enableTags, []));
      this.settingsForm.addControl('enableRelationships', new FormControl(settings.enableRelationships, []));
      this.settingsForm.addControl('enablePeople', new FormControl(settings.enablePeople, []));
      this.settingsForm.addControl('enableStartDate', new FormControl(settings.enableStartDate, []));
      this.settingsForm.addControl('enableCoverImage', new FormControl(settings.enableCoverImage, []));


      this.settingsForm.addControl('enableChapterTitle', new FormControl(settings.enableChapterTitle, []));
      this.settingsForm.addControl('enableChapterSummary', new FormControl(settings.enableChapterSummary, []));
      this.settingsForm.addControl('enableChapterReleaseDate', new FormControl(settings.enableChapterReleaseDate, []));
      this.settingsForm.addControl('enableChapterPublisher', new FormControl(settings.enableChapterPublisher, []));
      this.settingsForm.addControl('enableChapterCoverImage', new FormControl(settings.enableChapterCoverImage, []));


      this.settingsForm.addControl('enableVolumeCoverImage', new FormControl(settings.enableVolumeCoverImage, []));

      this.settingsForm.addControl('blacklist', new FormControl(settings.blacklist, []));
      this.settingsForm.addControl('whitelist', new FormControl(settings.whitelist, []));
      this.settingsForm.addControl('filterAboveWeight', new FormControl(settings.filterAboveWeight, []));


      this.settingsForm.addControl('globalLanguageTitleSettings', this.fb.group({
        name: [settings.globalLanguageTitleSettings.name, [languageCodeListValidator()]],
        localizedName: [settings.globalLanguageTitleSettings.localizedName, [languageCodeListValidator()]],
      }));

      this.settingsForm.addControl('libraryLanguageTitleOverrides', this.libraryLanguageOverrides);
      Object.entries(settings.libraryLanguageTitleOverrides || {}).forEach(([libraryId, override]) => {
        this.addLibraryLanguageOverride(parseInt(libraryId, 10), override);
      });


      this.settingsForm.addControl('externalAgeRatingMappings',
        buildAgeRatingMappingsArray(this.fb, settings.externalAgeRatingMappings));

      this.settingsForm.addControl('firstLastPeopleNaming', new FormControl((settings.firstLastPeopleNaming), []));
      this.settingsForm.addControl('personRoles', this.fb.group(
        Object.fromEntries(
          this.personRoles().map((role, index) => [
            `personRole_${index}`,
            this.fb.control((settings.personRoles || this.personRoles).includes(role)),
          ])
        )
      ));

      this.settingsForm.addControl('overrides', this.fb.group(
        Object.fromEntries(
          this.allMetadataSettingFields.map((role: MetadataSettingField, index: number) => [
            `override_${index}`,
            this.fb.control((settings.overrides || []).includes(role)),
          ])
        )
      ));

      this.settingsForm.get('enablePeople')?.valueChanges.subscribe(enabled => {
        const firstLastControl = this.settingsForm.get('firstLastPeopleNaming');
        if (enabled) {
          firstLastControl?.enable();
        } else {
          firstLastControl?.disable();
        }
      });

      this.settingsForm.get('enablePeople')?.updateValueAndValidity();

      // Disable personRoles checkboxes based on enablePeople state
      this.settingsForm.get('enablePeople')?.valueChanges.subscribe(enabled => {
        const personRolesArray = this.settingsForm.get('personRoles') as FormArray;
        if (enabled) {
          personRolesArray.enable();
        } else {
          personRolesArray.disable();
        }
      });

      this.isLoaded.set(true);


      this.settingsForm.valueChanges.pipe(
        takeUntilDestroyed(this.destroyRef),
      ).subscribe(() => this.cdRef.markForCheck());

      this.settingsForm.valueChanges.pipe(
        debounceTime(300),
        takeUntilDestroyed(this.destroyRef),
        filter(() => this.settingsForm.valid),
        map(_ => this.packData()),
        switchMap((data) => this.settingService.updateMetadataSettings(data)),
      ).subscribe();

    });

    this.serverService.isTaskRunning(TaskMethodNames.RunMetadataMappings, QueueNames.Scan).pipe(
      tap(b => this.isReRunInProgress.set(b))
    ).subscribe();

    this.messageHub.messages$.pipe(
      takeUntilDestroyed(this.destroyRef),
      filter(e => e.event === EVENTS.NotificationProgress),
      map(e => e.payload as NotificationProgressEvent),
      filter(e => e.name === EVENTS.RerunMetadataMappingsProgress),
      map(e => e.eventType !== 'ended'),
      tap(inProgress => this.isReRunInProgress.set(inProgress))
    ).subscribe();
  }

  packData(withFieldMappings: boolean = true) {
    const model = this.settingsForm.value;

    const exp: MetadataMappingsExport = this.manageMetadataMappingsComponent().packData()

    return {
      ...model,
      ageRatingMappings: exp.ageRatingMappings,
      fieldMappings: withFieldMappings ? exp.fieldMappings : [],
      blacklist: exp.blacklist,
      whitelist: exp.whitelist,
      personRoles: Object.entries(this.settingsForm.get('personRoles')!.value)
        .filter(([_, value]) => value)
        .map(([key, _]) => this.personRoles()[parseInt(key.split('_')[1], 10)]),
      overrides: Object.entries(this.settingsForm.get('overrides')!.value)
        .filter(([_, value]) => value)
        .map(([key, _]) => this.allMetadataSettingFields[parseInt(key.split('_')[1], 10)]),
      libraryLanguageTitleOverrides: this.packLibraryLanguageOverrides(),
      externalAgeRatingMappings: packAgeRatingMappings(this.settingsForm.get('externalAgeRatingMappings')?.value ?? []),
    }
  }

  private packLibraryLanguageOverrides(): Record<string, SeriesNameLanguage> {
    return this.libraryLanguageOverrides.controls.reduce((acc: Record<string, SeriesNameLanguage>, control) => {
      const {libraryId, name, localizedName} = control.value;
      if (!libraryId) return acc;
      if (!name && !localizedName) return acc;

      acc[libraryId] = {name: name || '', localizedName: localizedName || ''};
      return acc;
    }, {});
  }

  reRunMappings() {
    this.modalService.open(RunMetadataMappingsModalComponent, DefaultModalOptions);
  }

  addLibraryLanguageOverride(libraryId: number | null = null, override: SeriesNameLanguage | null = null) {
    this.libraryLanguageOverrides.push(this.fb.group({
      libraryId: [libraryId],
      name: [override?.name || '', [languageCodeListValidator()]],
      localizedName: [override?.localizedName || '', [languageCodeListValidator()]],
    }));
    this.cdRef.markForCheck();
  }

  removeLibraryLanguageOverride(index: number) {
    this.libraryLanguageOverrides.removeAt(index);
    this.cdRef.markForCheck();
  }

  availableLibrariesFor(index: number): Array<Library> {
    const claimed = new Set(
      this.libraryLanguageOverrides.controls
        .filter((_, i) => i !== index)
        .map(c => c.value.libraryId)
        .filter(id => id != null)
    );

    return this.libraries().filter(l => !claimed.has(l.id));
  }

  resolvedLanguageNames(codes: string | null | undefined): Array<string> {
    const titles = this.languageTitleByCode();
    return splitLanguageCodes(codes).map(code => {
      if (isNativeToken(code)) return translate('manage-metadata-settings.native-title-token-label');
      if (isRomajiToken(code)) return translate('manage-metadata-settings.romaji-title-token-label');

      return titles.get(code.toLowerCase()) || code;
    });
  }

  warningCodesFor(codes: string | null | undefined): Array<string> {
    return [
      ...unknownLanguageSubtags(codes, this.knownPrimarySubtags()),
      ...unknownScriptSubtags(codes),
    ];
  }

  protected readonly allMetadataSettingFields = allMetadataSettingField;
  protected readonly allTagWeights = allTagWeights;
  protected readonly SettingsTabId = SettingsTabId;
  protected readonly MangaBakaAgeRatings = MangaBakaAgeRatings;
}
