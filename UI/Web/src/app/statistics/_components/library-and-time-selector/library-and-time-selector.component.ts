import {ChangeDetectionStrategy, Component, computed, effect, inject, input, OnInit, signal} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {
  SmartTimeRangePickerComponent,
  TimeRange
} from "../../../shared/smart-time-range-picker/smart-time-range-picker.component";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {Library} from "../../../_models/library/library";
import {TypeaheadSettings} from "../../../typeahead/_models/typeahead-settings";
import {map} from "rxjs/operators";
import {StatsFilter} from "../../_models/stats-filter";
import {of, tap} from "rxjs";
import {LibraryService} from "../../../_services/library.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {UtilityService} from "../../../shared/_services/utility.service";
import {AccountService} from "../../../_services/account.service";

export interface LibraryAndTimeFilterGroup {
  timeFilter: FormGroup<{
    startDate: FormControl<Date | null>;
    endDate: FormControl<Date | null>;
  }>;
  libraries: FormControl<number[]>;
}

@Component({
  selector: 'app-library-and-time-selector',
  imports: [
    ReactiveFormsModule,
    SmartTimeRangePickerComponent,
    TypeaheadComponent,
    TranslocoDirective
  ],
  templateUrl: './library-and-time-selector.component.html',
  styleUrl: './library-and-time-selector.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LibraryAndTimeSelectorComponent implements OnInit {

  private readonly libraryService = inject(LibraryService);
  private readonly utilityService = inject(UtilityService);

  filterForm = input.required<FormGroup>();
  label = input.required<string>();
  userId = input.required<number>();

  allLibraries = signal<Library[]>([]);
  showLibraryTypeahead = signal(false);
  libraryTypeaheadSettings?: TypeaheadSettings<Library>;



  filter = signal<StatsFilter | undefined>(undefined);
  year = computed(() => this.filter()?.timeFilter.endDate?.getFullYear() ?? new Date().getFullYear());

  constructor() {
    effect(() => {
      const form = this.filterForm();
      form.valueChanges.pipe(
        map(value => value as StatsFilter),
      ).subscribe(value => this.filter.set(value));
    });
  }

  ngOnInit() {
    this.libraryService.getLibrariesForUser(this.userId()).pipe(
      tap(libs => this.allLibraries.set(libs)),
      tap(libs => this.filterForm().get('libraries')?.setValue(libs.map(l => l.id))),
      tap(libs => this.libraryTypeaheadSettings = this.setupLibrarySettings(libs, libs))
    ).subscribe();
  }

  setupLibrarySettings(
    allLibraries: Array<Library>,
    currentSelectedLibraries: Array<Library> | undefined,
  ): TypeaheadSettings<Library> {
    const settings = new TypeaheadSettings<Library>();

    settings.minCharacters = 0;
    settings.multiple = true;
    settings.id = 'libraries';
    settings.unique = true;
    settings.showLocked = false;
    settings.addIfNonExisting = false;
    settings.compareFn = (options: Library[], filter: string) => {
      return options.filter(l => this.utilityService.filter(l.name, filter));
    }
    settings.compareFnForAdd = (options: Library[], filter: string) => {
      return options.filter(l => this.utilityService.filterMatches(l.name, filter));
    }
    settings.fetchFn = (filter: string) => of(allLibraries)
      .pipe(map(items => settings.compareFn(items, filter)));

    settings.selectionCompareFn = (a: Library, b: Library) => {
      return a.id === b.id;
    }

    settings.trackByIdentityFn = (_, value) => value.id + '';

    const savedData = currentSelectedLibraries?.filter(l => allLibraries.indexOf(l) >= 0);
    if (savedData) {
      settings.savedData = savedData;
    }

    return settings;
  }


  updateSelectedLibraries(libs: Library[]) {
    this.filterForm().get('libraries')!.setValue(libs.map(l => l.id));
    this.libraryTypeaheadSettings = this.setupLibrarySettings(this.allLibraries(), libs);
  }

  updateTimeRange(tr: TimeRange) {
    this.filterForm().get('timeFilter')!.setValue(tr);
  }

  libraryName(libraryId: number): string {
    return this.allLibraries().find(l => l.id === libraryId)?.name ?? 'unknown';
  }

}
