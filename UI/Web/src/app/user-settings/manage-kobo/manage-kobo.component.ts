import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
  viewChild
} from '@angular/core';
import {takeUntilDestroyed, toObservable} from '@angular/core/rxjs-interop';
import {disabled, FieldTree, form, FormField, max, min, required} from '@angular/forms/signals';
import {TranslocoDirective} from '@jsverse/transloco';
import {debounceTime, distinctUntilChanged, EMPTY, filter, finalize, forkJoin, map, of, switchMap} from 'rxjs';
import {SettingsService} from '../../admin/settings.service';
import {AccountService} from '../../_services/account.service';
import {CollectionTagService} from '../../_services/collection-tag.service';
import {LibraryService} from '../../_services/library.service';
import {ReadingListService} from '../../_services/reading-list.service';
import {UtilityService} from '../../shared/_services/utility.service';
import {KoboSyncPreferences, Preferences} from '../../_models/preferences/preferences';
import {FormFieldDirective} from '../../_directives/form-field.directive';
import {SettingItemComponent} from '../../settings/_components/setting-item/setting-item.component';
import {SettingSwitchComponent} from '../../settings/_components/setting-switch/setting-switch.component';
import {TypeaheadComponent} from '../../typeahead/_components/typeahead.component';
import {TypeaheadConfig} from '../../typeahead/_models/typeahead-config';
import {LoadingComponent} from '../../shared/loading/loading.component';
import {EmptyStateComponent} from '../../shared/_components/empty-state/empty-state.component';
import {LicenseService} from "../../_services/license.service";

interface SyncSourceItem {
  id: number;
  title: string;
}

interface SyncSource {
  key: string;
  titleKey: string;
  tooltipKey: string;
  selectionLabelKey: string;
  enabled: FieldTree<boolean>;
  ids: FieldTree<number[]>;
  typeahead: TypeaheadConfig<SyncSourceItem>;
}

@Component({
  selector: 'app-manage-kobo',
  imports: [
    TranslocoDirective,
    FormField,
    FormFieldDirective,
    SettingItemComponent,
    SettingSwitchComponent,
    TypeaheadComponent,
    LoadingComponent,
    EmptyStateComponent
  ],
  templateUrl: './manage-kobo.component.html',
  styleUrl: './manage-kobo.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageKoboComponent implements OnInit {

  private readonly settingsService = inject(SettingsService);
  private readonly libraryService = inject(LibraryService);
  private readonly collectionTagService = inject(CollectionTagService);
  private readonly readingListService = inject(ReadingListService);
  private readonly utilityService = inject(UtilityService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly accountService = inject(AccountService);
  protected readonly licenseService = inject(LicenseService);

  protected readonly koboEnabledRsc = this.settingsService.getKoboEnabledResource();
  protected readonly koboEnabled = computed(() => this.koboEnabledRsc.isLoading() || this.koboEnabledRsc.value() === true);
  protected readonly koboOff = computed(() => this.koboEnabledRsc.value() === false);
  protected readonly koboEditable = computed(() => this.koboEnabled()
    && (this.accountService.hasAdminRole() || this.accountService.hasDownloadRole()));
  protected readonly isReadOnly = this.accountService.hasReadOnlyRole;

  protected readonly loading = signal(true);
  protected readonly preferencesError = signal(false);
  protected readonly syncSources = signal<SyncSource[]>([]);
  protected readonly managedCollectionTypeahead = signal<TypeaheadConfig<SyncSourceItem> | null>(null);

  private readonly managedCollectionTypeaheadRef = viewChild<TypeaheadComponent<SyncSourceItem>>('managedCollectionTypeahead');

  private readonly formModel = signal<KoboSyncPreferences>({
    collectionSyncEnabled: false,
    forwardToKoboStore: false,
    librarySyncEnabled: false,
    managedCollection: 0,
    readingListSyncEnabled: false,
    syncCollections: [],
    syncItemLimit: 25,
    syncLibraries: [],
    syncReadingLists: []
  });

  protected readonly formGroup = form(this.formModel, path => {
    required(path.syncItemLimit);
    min(path.syncItemLimit, 25);
    max(path.syncItemLimit, 100);

    disabled(path.syncItemLimit, {when: () => !this.koboEditable() || this.isReadOnly()});
    disabled(path.forwardToKoboStore, {when: () => !this.koboEditable() || this.isReadOnly()});
    disabled(path.librarySyncEnabled, {when: () => !this.koboEditable() || this.isReadOnly()});
    disabled(path.readingListSyncEnabled, {when: () => !this.koboEditable() || this.isReadOnly()});
    disabled(path.collectionSyncEnabled, {when: () => !this.koboEditable() || this.isReadOnly()});
    disabled(path.managedCollection, {when: () => !this.koboEditable() || this.isReadOnly()});
  });

  protected readonly managedCollectionId = computed(() => this.formModel().managedCollection);

  private lastPersistedPreferences = '';

  constructor() {
    toObservable(this.formModel).pipe(
      debounceTime(150),
      map(pref => JSON.stringify(pref)),
      distinctUntilChanged(),
      filter(json => !this.loading() && !this.preferencesError() && this.formGroup().valid()
        && json !== this.lastPersistedPreferences),
      map(json => JSON.parse(json) as KoboSyncPreferences),
      switchMap(pref => this.savePreferences(pref)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }

  ngOnInit(): void {
    this.loadPreferences();
  }

  protected onSourceSelectionChange(source: SyncSource, items: SyncSourceItem[]) {
    source.ids().value.set(items.map(item => item.id));
  }

  protected onManagedCollectionChange(items: SyncSourceItem[]) {
    this.formGroup.managedCollection().value.set(items[0]?.id ?? 0);
  }

  private loadPreferences() {
    this.loading.set(true);
    this.preferencesError.set(false);

    forkJoin({
      libraries: this.libraryService.getLibraries(),
      readingLists: this.readingListService.getReadingLists(),
      collections: this.collectionTagService.allCollections()
    }).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: ({libraries, readingLists, collections}) => {
        const preferences = this.accountService.userPreferences()?.koboSyncPreferences;
        if (!preferences) {
          this.preferencesError.set(true);
          return;
        }

        this.formModel.set(structuredClone(preferences));
        this.lastPersistedPreferences = JSON.stringify(preferences);

        const libraryItems = libraries.map(l => ({id: l.id, title: l.name}));
        const readingListItems = (readingLists.result ?? []).map(r => ({id: r.id, title: r.title}));
        const collectionItems = collections.map(c => ({id: c.id, title: c.title}));

        this.syncSources.set([
          {
            key: 'libraries',
            titleKey: 'sync-libraries-label',
            tooltipKey: 'sync-libraries-tooltip',
            selectionLabelKey: 'sync-libraries-selection-label',
            enabled: this.formGroup.librarySyncEnabled,
            ids: this.formGroup.syncLibraries,
            typeahead: this.buildTypeahead('kobo-sync-libraries', libraryItems, preferences.syncLibraries)
          },
          {
            key: 'reading-lists',
            titleKey: 'sync-reading-lists-label',
            tooltipKey: 'sync-reading-lists-tooltip',
            selectionLabelKey: 'sync-reading-lists-selection-label',
            enabled: this.formGroup.readingListSyncEnabled,
            ids: this.formGroup.syncReadingLists,
            typeahead: this.buildTypeahead('kobo-sync-reading-lists', readingListItems, preferences.syncReadingLists)
          },
          {
            key: 'collections',
            titleKey: 'sync-collections-label',
            tooltipKey: 'sync-collections-tooltip',
            selectionLabelKey: 'sync-collections-selection-label',
            enabled: this.formGroup.collectionSyncEnabled,
            ids: this.formGroup.syncCollections,
            typeahead: this.buildTypeahead('kobo-sync-collections', collectionItems, preferences.syncCollections)
          }
        ]);

        const managedCollectionConfig = this.buildTypeahead('kobo-managed-collection', collectionItems,
          preferences.managedCollection > 0 ? [preferences.managedCollection] : []);
        managedCollectionConfig.multiple = false;
        this.managedCollectionTypeahead.set(managedCollectionConfig);
      },
      error: () => this.preferencesError.set(true)
    });
  }

  private savePreferences(preferences: KoboSyncPreferences) {
    const current = this.accountService.userPreferences();
    if (!current) return EMPTY;

    this.lastPersistedPreferences = JSON.stringify(preferences);
    const updated: Preferences = {...current, koboSyncPreferences: {...preferences}};
    return this.accountService.updatePreferences(updated);
  }

  private buildTypeahead(id: string, items: SyncSourceItem[], selectedIds: number[]): TypeaheadConfig<SyncSourceItem> {
    const config = new TypeaheadConfig<SyncSourceItem>();
    config.id = id;
    config.multiple = true;
    config.unique = true;
    config.minCharacters = 0;
    config.addIfNonExisting = false;
    config.trackByIdentityFn = (_, item) => `${item.id}`;
    config.titleFn = (item) => item.title;
    config.selectionCompareFn = (a, b) => a.id === b.id;
    config.compareFn = (options, filter) => options.filter(option => this.utilityService.filter(option.title, filter));
    config.fetchFn = (filter) => of(config.compareFn(items, filter));
    config.savedData = items.filter(item => selectedIds.includes(item.id));
    return config;
  }
}
