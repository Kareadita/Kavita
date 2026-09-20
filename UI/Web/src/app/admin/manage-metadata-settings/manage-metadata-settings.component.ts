import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal, viewChild} from '@angular/core';
import {NgTemplateOutlet} from "@angular/common";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {SettingsService} from "../settings.service";
import {debounceTime, filter, switchMap} from "rxjs";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {map, tap} from "rxjs/operators";
import {MetadataSettings, SeriesNameLanguage} from "../_models/metadata-settings";
import {PersonRole} from "../../_models/metadata/person";
import {PersonRolePipe} from "../../_pipes/person-role.pipe";
import {allMetadataSettingField} from "../_models/metadata-setting-field";
import {MetadataSettingFiledPipe} from "../../_pipes/metadata-setting-filed.pipe";
import {
  ManageMetadataMappingsComponent,
  MetadataMappingsFormModel,
  metadataMappingsSchema,
  packMetadataMappings,
  toMetadataMappingsFormModel
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
import {MetadataService} from "../../_services/metadata.service";
import {LibraryService} from "../../_services/library.service";
import {Library} from "../../_models/library/library";
import {Language} from "../../_models/metadata/language";
import {
  isNativeToken,
  isReservedToken,
  isRomajiToken,
  isWellFormedLanguageCode,
  primarySubtag,
  splitLanguageCodes,
  unknownLanguageSubtags,
  unknownScriptSubtags
} from "../../shared/utils/language-code.util";
import {
  apply,
  applyEach,
  disabled,
  FieldTree,
  form,
  FormField,
  FormRoot,
  PathKind,
  required,
  SchemaPath,
  SchemaPathRules,
  validate
} from "@angular/forms/signals";
import {
  EnumOption,
  SettingSelectComponent
} from "../../settings/_components/setting-enum-select/setting-select.component";

interface LibraryLanguageOverrideFormModel {
  libraryId: number | null;
  name: string;
  localizedName: string;
}

interface FormModel {
  enabled: boolean;
  enableExtendedMetadataProcessing: boolean;

  enableSummary: boolean;
  enableLocalizedName: boolean;
  enableName: boolean;
  enablePublicationStatus: boolean;
  enableAgeRating: boolean;
  enableRelationships: boolean;
  enablePeople: boolean;
  enableStartDate: boolean;
  enableCoverImage: boolean;

  enableChapterTitle: boolean;
  enableChapterSummary: boolean;
  enableChapterReleaseDate: boolean;
  enableChapterPublisher: boolean;
  enableChapterCoverImage: boolean;

  enableVolumeCoverImage: boolean;

  firstLastPeopleNaming: boolean;

  globalLanguageTitleSettings: SeriesNameLanguage;
  libraryLanguageTitleOverrides: Array<LibraryLanguageOverrideFormModel>;

  /** Keyed by {@link PersonRole}, true when that role is written */
  personRoles: Record<string, boolean>;
  /** Keyed by {@link MetadataSettingField}, true when Kavita's own value is overwritten */
  overrides: Record<string, boolean>;

  /** Owned by {@link ManageMetadataMappingsComponent} */
  mappings: MetadataMappingsFormModel;
}

const MalformedLanguageCodes = 'malformedLanguageCodes';

/**
 * Error tier for a semicolon separated BCP-47 priority list. Flags only malformed codes, so unrecognized
 * (but well-formed) languages and scripts stay warnings surfaced by {@link ManageMetadataSettingsComponent.warningCodesFor}.
 *
 * Stays local to this component as the language priority lists are the only place this shape exists.
 */
function languageCodeList<TPathKind extends PathKind = PathKind.Root>(
  path: SchemaPath<string, SchemaPathRules.Supported, TPathKind>
) {
  validate(path, ({value}) => {
    const malformed = splitLanguageCodes(value())
      .filter(c => !isWellFormedLanguageCode(c) && !isReservedToken(c));

    if (malformed.length === 0) return null;

    return {
      kind: MalformedLanguageCodes,
      message: translate('manage-metadata-settings.language-code-malformed', {codes: malformed.join(', ')})
    };
  });
}


@Component({
  selector: 'app-manage-metadata-settings',
  imports: [
    TranslocoDirective,
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
    NgTemplateOutlet,
    FormField,
    FormRoot,
    SettingSelectComponent,
  ],
  templateUrl: './manage-metadata-settings.component.html',
  styleUrl: './manage-metadata-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMetadataSettingsComponent implements OnInit {

  readonly manageMetadataMappingsComponent = viewChild(ManageMetadataMappingsComponent);

  private readonly settingService = inject(SettingsService);
  private readonly metadataService = inject(MetadataService);
  private readonly libraryService = inject(LibraryService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly modalService = inject(ModalService);
  private readonly messageHub = inject(MessageHubService);
  private readonly serverService = inject(ServerService);

  private readonly formModel = signal<FormModel>({
    enabled: false,
    enableExtendedMetadataProcessing: false,

    enableSummary: false,
    enableLocalizedName: false,
    enableName: false,
    enablePublicationStatus: false,
    enableAgeRating: false,
    enableRelationships: false,
    enablePeople: false,
    enableStartDate: false,
    enableCoverImage: false,

    enableChapterTitle: false,
    enableChapterSummary: false,
    enableChapterReleaseDate: false,
    enableChapterPublisher: false,
    enableChapterCoverImage: false,

    enableVolumeCoverImage: false,

    firstLastPeopleNaming: false,

    globalLanguageTitleSettings: {name: '', localizedName: ''},
    libraryLanguageTitleOverrides: [],

    personRoles: {},
    overrides: {},

    mappings: {
      enableGenres: false,
      enableTags: false,
      filterAboveWeight: null,
      blacklist: [],
      whitelist: [],
      ageRatingMappings: [],
      externalAgeRatingMappings: [],
      fieldMappings: [],
    },
  });
  protected readonly formGroup = form(this.formModel, p => {
    languageCodeList(p.globalLanguageTitleSettings.name);
    languageCodeList(p.globalLanguageTitleSettings.localizedName);

    applyEach(p.libraryLanguageTitleOverrides, row => {
      required(row.libraryId);
      languageCodeList(row.name);
      languageCodeList(row.localizedName);
    });

    disabled(p.firstLastPeopleNaming, {when: ({valueOf}) => !valueOf(p.enablePeople)});
    disabled(p.personRoles, {when: ({valueOf}) => !valueOf(p.enablePeople)});

    apply(p.mappings, metadataMappingsSchema);
  });

  personRoles = signal<PersonRole[]>([PersonRole.Writer, PersonRole.CoverArtist, PersonRole.Character]);
  isLoaded = signal<boolean>(false);


  isReRunInProgress = signal(true);

  libraries = signal<Array<Library>>([]);
  bcp47Languages = signal<Array<Language>>([]);

  protected readonly canAddLibraryOverride = computed(
    () => this.formModel().libraryLanguageTitleOverrides.length < this.libraries().length
  );

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

  constructor() {
    // The page autosaves on every change
    toObservable(this.formModel).pipe(
      filter(() => this.isLoaded()),
      debounceTime(300),
      filter(() => this.formGroup().valid()),
      map(() => this.packData()),
      switchMap((data) => this.settingService.updateMetadataSettings(data)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  ngOnInit(): void {
    this.metadataService.getAllBcp47Languages().subscribe(languages => {
      this.bcp47Languages.set(languages);
    });

    this.libraryService.getLibraries().subscribe(libraries => {
      this.libraries.set(libraries);
    });

    this.settingService.getMetadataSettings().subscribe(settings => {
      this.formModel.set(this.toFormModel(settings));
      this.isLoaded.set(true);
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

  private toFormModel(settings: MetadataSettings): FormModel {
    return {
      enabled: settings.enabled,
      enableExtendedMetadataProcessing: settings.enableExtendedMetadataProcessing,

      enableSummary: settings.enableSummary,
      enableLocalizedName: settings.enableLocalizedName,
      enableName: settings.enableName,
      enablePublicationStatus: settings.enablePublicationStatus,
      enableAgeRating: settings.enableAgeRating,
      enableRelationships: settings.enableRelationships,
      enablePeople: settings.enablePeople,
      enableStartDate: settings.enableStartDate,
      enableCoverImage: settings.enableCoverImage,

      enableChapterTitle: settings.enableChapterTitle,
      enableChapterSummary: settings.enableChapterSummary,
      enableChapterReleaseDate: settings.enableChapterReleaseDate,
      enableChapterPublisher: settings.enableChapterPublisher,
      enableChapterCoverImage: settings.enableChapterCoverImage,

      enableVolumeCoverImage: settings.enableVolumeCoverImage,

      firstLastPeopleNaming: settings.firstLastPeopleNaming,

      globalLanguageTitleSettings: {
        name: settings.globalLanguageTitleSettings?.name || '',
        localizedName: settings.globalLanguageTitleSettings?.localizedName || '',
      },
      libraryLanguageTitleOverrides: Object.entries(settings.libraryLanguageTitleOverrides || {})
        .map(([libraryId, override]) => ({
          libraryId: parseInt(libraryId, 10),
          name: override.name || '',
          localizedName: override.localizedName || '',
        })),

      personRoles: Object.fromEntries(
        this.personRoles().map(role => [role, (settings.personRoles || this.personRoles()).includes(role)])
      ),
      overrides: Object.fromEntries(
        this.allMetadataSettingFields.map(field => [field, (settings.overrides || []).includes(field)])
      ),

      mappings: toMetadataMappingsFormModel(settings),
    };
  }

  packData(withFieldMappings: boolean = true): MetadataSettings {
    const {mappings, ...model} = this.formModel();

    return {
      ...model,
      ...packMetadataMappings(mappings, withFieldMappings),
      personRoles: this.packCheckedKeys(model.personRoles),
      overrides: this.packCheckedKeys(model.overrides),
      libraryLanguageTitleOverrides: this.packLibraryLanguageOverrides(this.formModel()),
    }
  }

  /**
   * Record keys are stringified enum members, the API wants the numbers back
   */
  private packCheckedKeys(checked: Record<string, boolean>): Array<number> {
    return Object.entries(checked)
      .filter(([_, isChecked]) => isChecked)
      .map(([key, _]) => parseInt(key, 10));
  }

  private packLibraryLanguageOverrides(model: FormModel): Record<string, SeriesNameLanguage> {
    return model.libraryLanguageTitleOverrides.reduce((acc: Record<string, SeriesNameLanguage>, row) => {
      const {libraryId, name, localizedName} = row;
      if (!libraryId) return acc;
      if (!name && !localizedName) return acc;

      acc[libraryId] = {name: name || '', localizedName: localizedName || ''};
      return acc;
    }, {});
  }

  reRunMappings() {
    this.modalService.open(RunMetadataMappingsModalComponent, DefaultModalOptions);
  }

  addLibraryLanguageOverride() {
    this.formGroup.libraryLanguageTitleOverrides().value.update(
      rows => [...rows, {libraryId: null, name: '', localizedName: ''}]
    );
  }

  removeLibraryLanguageOverride(index: number) {
    this.formGroup.libraryLanguageTitleOverrides().value.update(rows => rows.filter((_, i) => i !== index));
  }

  /**
   * The libraries still selectable in a given row, so no two overrides can claim the same library.
   */
  libraryOptionsFor(index: number): Array<EnumOption<number>> {
    const claimed = new Set(
      this.formModel().libraryLanguageTitleOverrides
        .filter((_, i) => i !== index)
        .map(row => row.libraryId)
        .filter(id => id !== null)
    );

    return this.libraries()
      .filter(l => !claimed.has(l.id))
      .map(l => ({value: l.id, title: l.name}));
  }

  /**
   * The localized malformed-code message on a language priority field, or null when every code is well-formed.
   */
  malformedCodeMessage(field: FieldTree<string>): string | null {
    return field().errors().find(e => e.kind === MalformedLanguageCodes)?.message ?? null;
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
  protected readonly SettingsTabId = SettingsTabId;
}
