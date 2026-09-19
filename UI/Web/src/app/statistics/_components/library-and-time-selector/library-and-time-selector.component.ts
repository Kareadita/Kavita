import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  HostListener,
  inject,
  input,
  OnInit,
  output,
  signal
} from '@angular/core';
import {
  SmartTimeRangePickerComponent,
  TimeRange
} from "../../../shared/smart-time-range-picker/smart-time-range-picker.component";
import {TypeaheadComponent} from "../../../typeahead/_components/typeahead.component";
import {Library} from "../../../_models/library/library";
import {TypeaheadConfig} from "../../../typeahead/_models/typeahead-config";
import {StatsFilter} from "../../_models/stats-filter";
import {tap} from "rxjs";
import {LibraryService} from "../../../_services/library.service";
import {TranslocoDirective} from "@jsverse/transloco";
import {ReaderService} from "../../../_services/reader.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TypeaheadConfigFactoryService} from "../../../typeahead-config-factory.service";

@Component({
  selector: 'app-library-and-time-selector',
  imports: [
    SmartTimeRangePickerComponent,
    TypeaheadComponent,
    TranslocoDirective
  ],
  templateUrl: './library-and-time-selector.component.html',
  styleUrl: './library-and-time-selector.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LibraryAndTimeSelectorComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly libraryService = inject(LibraryService);
  private readonly readerService = inject(ReaderService);
  private readonly elementRef = inject(ElementRef);
  private readonly typeaheadSettingFactoryService = inject(TypeaheadConfigFactoryService);

  label = input.required<string>();
  userId = input.required<number>();
  locale = input<'server' | 'profile'>('profile');

  filterChange = output<StatsFilter>();
  yearChange = output<number>();

  startYear = signal(new Date().getFullYear())
  allLibraries = signal<Library[]>([]);
  showLibraryTypeahead = signal(false);
  libraryTypeaheadSettings = signal<TypeaheadConfig<Library> | undefined>(undefined);

  filter = signal<StatsFilter>({
    timeFilter: {startDate: null, endDate: null},
    libraries: [],
    timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
  });
  year = computed(() => this.filter().timeFilter.endDate?.getFullYear() ?? new Date().getFullYear());


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

    effect(() => {
      this.filterChange.emit(this.filter());
      this.yearChange.emit(this.year());
    });

  }

  ngOnInit() {
    this.libraryService.getLibrariesForUser(this.userId()).pipe(
      tap(libs => this.allLibraries.set(libs)),
      tap(libs => this.filter.update(f => ({...f, libraries: libs.map(l => l.id)}))),
      tap(libs => this.libraryTypeaheadSettings.set(this.setupLibrarySettings(libs, libs))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();

    this.readerService.getFirstProgressDateForUser(this.userId()).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(date => {
      const jsDate = new Date(date);
      this.startYear.set(jsDate.getFullYear());
    });
  }

  setupLibrarySettings(
    allLibraries: Array<Library>,
    currentSelectedLibraries: Array<Library> | undefined,
  ): TypeaheadConfig<Library> {
    return this.typeaheadSettingFactoryService.forLibraries({id: 'libraries', libraries: allLibraries,
      overrides: {
      showLocked: false,
        savedData: currentSelectedLibraries?.filter(l => allLibraries.indexOf(l) >= 0)
      }
    });
  }

  updateSelectedLibraries(libs: Library[]) {
    this.filter.update(f => ({...f, libraries: libs.map(l => l.id)}));
    this.libraryTypeaheadSettings.set(this.setupLibrarySettings(this.allLibraries(), libs));
  }

  updateTimeRange(tr: TimeRange) {
    this.filter.update(f => ({...f, timeFilter: tr}));
  }

  libraryName(libraryId: number): string {
    return this.allLibraries().find(l => l.id === libraryId)?.name ?? 'unknown';
  }

}
