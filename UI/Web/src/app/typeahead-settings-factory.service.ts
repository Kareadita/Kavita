import {inject, Injectable} from '@angular/core';
import {TypeaheadSettings} from "./typeahead/_models/typeahead-settings";
import {Person, PersonRole} from "./_models/metadata/person";
import {map} from "rxjs/operators";
import {UtilityService} from "./shared/_services/utility.service";
import {MetadataService} from "./_services/metadata.service";
import {Chapter} from "./_models/chapter";

/**
 * Partial configuration for overrides. All properties optional.
 *
 * Note: setting addIfNonExisting: true is only valid when the factory method you
 * called also supplies compareFnForAdd and addTransformFn — the typeahead asserts
 * both non-null at runtime. Prefer the addIfNonExisting param where one exists.
 */
export type TypeaheadConfigurationOverrides<T> = Partial<TypeaheadSettings<T>>;

export interface TypeaheadFactoryParameters<T> {
  /**
   * Id of the input element, for linking label elements (accessibility). Required — no sensible default.
   */
  id: string;
  /**
   * Data to preload the typeahead with. Also settable via overrides.savedData (overrides win)
   * or by assigning to the returned instance once an async fetch resolves.
   */
  savedData?: T[] | T;
  overrides?: TypeaheadConfigurationOverrides<T>;
}

export interface TypeaheadFactoryPersonParameters extends TypeaheadFactoryParameters<Person> {
  /**
   * Captured by addTransformFn to stamp the role onto newly-added people.
   */
  role?: PersonRole;
  addIfNonExisting?: boolean;
}

export interface TypeaheadFactoryChapterParameters extends TypeaheadFactoryParameters<Chapter> {
  /**
   * Captured by fetchFn and by the generated id suffix.
   */
  seriesId: number;
}

@Injectable({providedIn: 'root'})
export class TypeaheadSettingsFactoryService {

  private readonly utilityService = inject(UtilityService);
  private readonly metadataService = inject(MetadataService);


  // Custom items: fetchFn, role, addIfNonExisting, multiple
  forPerson(params: TypeaheadFactoryPersonParameters) {
    const {id, role, addIfNonExisting = true, savedData, overrides} = params;

    const settings = new TypeaheadSettings<Person>();
    settings.id = id;
    settings.minCharacters = 0;
    settings.multiple = true;
    settings.showLocked = true;
    settings.unique = true;
    settings.addIfNonExisting = addIfNonExisting;

    settings.compareFn = (options, filter) => options.filter(m => this.utilityService.filter(m.name, filter));
    settings.compareFnForAdd = (options, filter) => options.filter(m => this.utilityService.filterMatches(m.name,
      filter));
    settings.selectionCompareFn = (a, b) => a.name === b.name;

    settings.fetchFn = (filter) => this.metadataService.getAllPeople()
      .pipe(map(items => settings.compareFn(items, filter)));

    settings.addTransformFn = (title) => {
      const newPerson = {id: 0, name: title, aliases: [], description: '', coverImage: '',
        coverImageLocked: false, primaryColor: '', secondaryColor: ''};
      return role ? {role, ...newPerson} : newPerson;
    };

    settings.trackByIdentityFn = (_, value) => value.name + value.id;

    if (savedData !== undefined) settings.savedData = savedData;

    return this.applyOverrides(settings, overrides);

  }

  /**
   * Applies overrides onto the settings instance. Mutates rather than spreading so that
   * closures created in the factory (fetchFn reading settings.compareFn) observe the
   * overridden values. Undefined values are skipped so they can't clobber class defaults.
   */
  private applyOverrides<T>(
    settings: TypeaheadSettings<T>,
    overrides?: TypeaheadConfigurationOverrides<T>
  ): TypeaheadSettings<T> {
    if (!overrides) return settings;

    const defined = Object.fromEntries(
      Object.entries(overrides).filter(([, value]) => value !== undefined)
    );
    Object.assign(settings, defined);

    return settings;
  }
}
