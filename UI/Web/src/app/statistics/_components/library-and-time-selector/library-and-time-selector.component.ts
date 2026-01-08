import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  HostListener,
  inject,
  input,
  OnInit,
  output,
  signal
} from '@angular/core';
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
import {ReaderService} from "../../../_services/reader.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

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
  private readonly readerService = inject(ReaderService);
  private readonly elementRef = inject(ElementRef);

  label = input.required<string>();
  userId = input.required<number>();
  locale = input<'server' | 'profile'>('profile');

  filterChange = output<StatsFilter>();
  yearChange = output<number>();

  startYear = signal(new Date().getFullYear())
  allLibraries = signal<Library[]>([]);
  showLibraryTypeahead = signal(false);
  libraryTypeaheadSettings?: TypeaheadSettings<Library>;
  protected filterForm = new FormGroup<LibraryAndTimeFilterGroup>({
    timeFilter: new FormGroup({
      startDate: new FormControl<Date | null>(null),
      endDate: new FormControl<Date | null>(null),
    }),
    libraries: new FormControl<number[]>([], { nonNullable: true }),
  });


  filter = signal<StatsFilter | undefined>(undefined);
  year = computed(() => this.filter()?.timeFilter.endDate?.getFullYear() ?? new Date().getFullYear());


  @HostListener('body:click', ['$event'])
  handleDocumentClick(event: Event) {

    const target = event.target as HTMLElement;

    if (!this.showLibraryTypeahead()) return;

    // Typeahead will click on the body to prevent multiple instances being open, it's impossible for user to click body
    if (target.tagName.toLowerCase() === 'body') return;

    // composedPath() returns the path at event dispatch time,
    // even if nodes are later removed
    const path = event.composedPath() as HTMLElement[];

    const clickedLibSelector = path.some(el =>
      el instanceof HTMLElement && el.classList.contains('lib-selector')
    );

    if (clickedLibSelector) {
      return; // We are toggling into the typeahead
    }

    const typeaheadInStack = path.some(el =>
      el.tagName?.toLowerCase() === 'app-typeahead'
    );

    if (!typeaheadInStack) {
      this.showLibraryTypeahead.set(false);
      return; // We clicked near the typeahead, so close
    }

    const clickedInElement = path.includes(this.elementRef.nativeElement);
    if (!clickedInElement) {
      this.showLibraryTypeahead.set(false);
      return;
    }
  }


  constructor() {

    this.filterForm.valueChanges.pipe(
      takeUntilDestroyed(),
    ).subscribe(value => {
      const filter = value as StatsFilter;
      filter.timezone = Intl.DateTimeFormat().resolvedOptions().timeZone;
      
      this.filterChange.emit(filter);
      this.yearChange.emit(filter.timeFilter?.endDate?.getFullYear() ?? new Date().getFullYear());
    });

  }

  ngOnInit() {
    this.libraryService.getLibrariesForUser(this.userId()).pipe(
      tap(libs => this.allLibraries.set(libs)),
      tap(libs => this.filterForm.get('libraries')?.setValue(libs.map(l => l.id))),
      tap(libs => this.libraryTypeaheadSettings = this.setupLibrarySettings(libs, libs))
    ).subscribe();

    this.readerService.getFirstProgressDateForUser(this.userId()).subscribe(date => {
      const jsDate = new Date(date);
      this.startYear.set(jsDate.getFullYear());
    });
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
    this.filterForm.get('libraries')!.setValue(libs.map(l => l.id));
    this.libraryTypeaheadSettings = this.setupLibrarySettings(this.allLibraries(), libs);
  }

  updateTimeRange(tr: TimeRange) {
    this.filterForm.get('timeFilter')!.setValue(tr);
  }

  libraryName(libraryId: number): string {
    return this.allLibraries().find(l => l.id === libraryId)?.name ?? 'unknown';
  }

}
