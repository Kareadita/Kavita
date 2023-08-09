import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild, DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import { FormControl, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { NgbCollapse, NgbTooltip, NgbRating } from '@ng-bootstrap/ng-bootstrap';
import { distinctUntilChanged, forkJoin, map, Observable, of, ReplaySubject } from 'rxjs';
import { FilterUtilitiesService } from '../shared/_services/filter-utilities.service';
import { Breakpoint, UtilityService } from '../shared/_services/utility.service';
import { TypeaheadSettings } from '../typeahead/_models/typeahead-settings';
import { CollectionTag } from '../_models/collection-tag';
import { Genre } from '../_models/metadata/genre';
import { Library } from '../_models/library';
import { MangaFormat } from '../_models/manga-format';
import { AgeRatingDto } from '../_models/metadata/age-rating-dto';
import { Language } from '../_models/metadata/language';
import { PublicationStatusDto } from '../_models/metadata/publication-status-dto';
import { Person, PersonRole } from '../_models/metadata/person';
import { FilterEvent, FilterItem, mangaFormatFilters, SeriesFilter, SortField } from '../_models/metadata/series-filter';
import { Tag } from '../_models/tag';
import { CollectionTagService } from '../_services/collection-tag.service';
import { LibraryService } from '../_services/library.service';
import { MetadataService } from '../_services/metadata.service';
import { ToggleService } from '../_services/toggle.service';
import { FilterSettings } from './filter-settings';
import { SeriesFilterV2 } from '../_models/metadata/v2/series-filter-v2';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { TypeaheadComponent } from '../typeahead/_components/typeahead.component';
import { DrawerComponent } from '../shared/drawer/drawer.component';
import { NgIf, NgTemplateOutlet, AsyncPipe } from '@angular/common';
import {TranslocoModule} from "@ngneat/transloco";
import {SortFieldPipe} from "../pipe/sort-field.pipe";
import {MetadataBuilderComponent} from "./_components/metadata-builder/metadata-builder.component";

@Component({
    selector: 'app-metadata-filter',
    templateUrl: './metadata-filter.component.html',
    styleUrls: ['./metadata-filter.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgIf, NgbCollapse, NgTemplateOutlet, DrawerComponent, NgbTooltip, TypeaheadComponent,
    ReactiveFormsModule, FormsModule, NgbRating, AsyncPipe, TranslocoModule, SortFieldPipe, MetadataBuilderComponent]
})
export class MetadataFilterComponent implements OnInit {

  /**
   * This toggles the opening/collapsing of the metadata filter code
   */
  @Input() filterOpen: EventEmitter<boolean> = new EventEmitter();

  /**
   * Should filtering be shown on the page
   */
  @Input() filteringDisabled: boolean = false;

  @Input({required: true}) filterSettings!: FilterSettings;

  @Output() applyFilter: EventEmitter<FilterEvent> = new EventEmitter();

  @ContentChild('[ngbCollapse]') collapse!: NgbCollapse;
  private readonly destroyRef = inject(DestroyRef);


   /**
   * Controls the visibility of extended controls that sit below the main header.
   */
  filteringCollapsed: boolean = true;

  filter!: SeriesFilter;
  libraries: Array<FilterItem<Library>> = [];


  readProgressGroup!: FormGroup;
  sortGroup!: FormGroup;
  seriesNameGroup!: FormGroup;
  releaseYearRange!: FormGroup;
  isAscendingSort: boolean = true;

  updateApplied: number = 0;

  fullyLoaded: boolean = false;
  filterV2: SeriesFilterV2 | undefined;



  handleFilters(filter: SeriesFilterV2) {
    console.log('[metadata-filter] handleFilters');
    this.filterV2 = filter;
  }


  private readonly cdRef = inject(ChangeDetectorRef);


  get SortField(): typeof SortField {
    return SortField;
  }

  constructor(private utilityService: UtilityService,
    public toggleService: ToggleService,
    private filterUtilityService: FilterUtilitiesService) {
  }

  ngOnInit(): void {
    if (this.filterSettings === undefined) {
      this.filterSettings = new FilterSettings();
      this.cdRef.markForCheck();
    }

    if (this.filterOpen) {
      this.filterOpen.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(openState => {
        this.filteringCollapsed = !openState;
        this.toggleService.set(!this.filteringCollapsed);
        this.cdRef.markForCheck();
      });
    }

    // this.filter = this.filterUtilityService.createSeriesFilter();
    // this.readProgressGroup = new FormGroup({
    //   read: new FormControl({value: this.filter.readStatus.read, disabled: this.filterSettings.readProgressDisabled}, []),
    //   notRead: new FormControl({value: this.filter.readStatus.notRead, disabled: this.filterSettings.readProgressDisabled}, []),
    //   inProgress: new FormControl({value: this.filter.readStatus.inProgress, disabled: this.filterSettings.readProgressDisabled}, []),
    // });
    this.filterV2 = this.filterSettings.presetsV2;
    console.log('filterV2: ', this.filterV2);



    console.log('this.filter.sortOptions: ', this.filter?.sortOptions);
    this.sortGroup = new FormGroup({
      sortField: new FormControl({value: this.filter?.sortOptions?.sortField || SortField.SortName, disabled: this.filterSettings.sortDisabled}, []),
    });

    this.fullyLoaded = true;
    this.cdRef.markForCheck();
    this.apply();

    // this.seriesNameGroup = new FormGroup({
    //   seriesNameQuery: new FormControl({value: this.filter.seriesNameQuery || '', disabled: this.filterSettings.searchNameDisabled}, [])
    // });

    // this.releaseYearRange = new FormGroup({
    //   min: new FormControl({value: undefined, disabled: this.filterSettings.releaseYearDisabled}, [Validators.min(1000), Validators.max(9999), Validators.maxLength(4), Validators.minLength(4)]),
    //   max: new FormControl({value: undefined, disabled: this.filterSettings.releaseYearDisabled}, [Validators.min(1000), Validators.max(9999), Validators.maxLength(4), Validators.minLength(4)])
    // });

    // this.readProgressGroup.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(changes => {
    //   this.filter.readStatus.read = this.readProgressGroup.get('read')?.value;
    //   this.filter.readStatus.inProgress = this.readProgressGroup.get('inProgress')?.value;
    //   this.filter.readStatus.notRead = this.readProgressGroup.get('notRead')?.value;
    //
    //   let sum = 0;
    //   sum += (this.filter.readStatus.read ? 1 : 0);
    //   sum += (this.filter.readStatus.inProgress ? 1 : 0);
    //   sum += (this.filter.readStatus.notRead ? 1 : 0);
    //
    //   if (sum === 1) {
    //     if (this.filter.readStatus.read) this.readProgressGroup.get('read')?.disable({ emitEvent: false });
    //     if (this.filter.readStatus.notRead) this.readProgressGroup.get('notRead')?.disable({ emitEvent: false });
    //     if (this.filter.readStatus.inProgress) this.readProgressGroup.get('inProgress')?.disable({ emitEvent: false });
    //   } else {
    //     this.readProgressGroup.get('read')?.enable({ emitEvent: false });
    //     this.readProgressGroup.get('notRead')?.enable({ emitEvent: false });
    //     this.readProgressGroup.get('inProgress')?.enable({ emitEvent: false });
    //   }
    //   this.cdRef.markForCheck();
    // });

    this.sortGroup.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(changes => {
      if (this.filter.sortOptions == null) {
        this.filter.sortOptions = {
          isAscending: this.isAscendingSort,
          sortField: parseInt(this.sortGroup.get('sortField')?.value, 10)
        };
      }
      this.filter.sortOptions.sortField = parseInt(this.sortGroup.get('sortField')?.value, 10);
      this.cdRef.markForCheck();
    });

    // this.seriesNameGroup.get('seriesNameQuery')?.valueChanges.pipe(
    //   map(val => (val || '').trim()),
    //   distinctUntilChanged(),
    //   takeUntilDestroyed(this.destroyRef)
    // )
    // .subscribe(changes => {
    //   this.filter.seriesNameQuery = changes; // TODO: See if we can make this into observable
    //   this.cdRef.markForCheck();
    // });

    // this.releaseYearRange.valueChanges.pipe(
    //   distinctUntilChanged(),
    //   takeUntilDestroyed(this.destroyRef)
    // )
    // .subscribe(changes => {
    //   this.filter.releaseYearRange = {min: this.releaseYearRange.get('min')?.value, max: this.releaseYearRange.get('max')?.value};
    //   this.cdRef.markForCheck();
    // });

    this.loadFromPresetsAndSetup();
  }

  close() {
    this.filterOpen.emit(false);
    this.filteringCollapsed = true;
    this.toggleService.set(!this.filteringCollapsed);
    this.cdRef.markForCheck();
  }


  loadFromPresetsAndSetup() {
    this.fullyLoaded = false;
    // if (this.filterSettings.presets) {
    //   this.readProgressGroup.get('read')?.patchValue(this.filterSettings.presets.readStatus.read);
    //   this.readProgressGroup.get('notRead')?.patchValue(this.filterSettings.presets.readStatus.notRead);
    //   this.readProgressGroup.get('inProgress')?.patchValue(this.filterSettings.presets.readStatus.inProgress);
    //
    //   if (this.filterSettings.presets.sortOptions) {
    //     this.sortGroup.get('sortField')?.setValue(this.filterSettings.presets.sortOptions.sortField);
    //     this.isAscendingSort = this.filterSettings.presets.sortOptions.isAscending;
    //     if (this.filter.sortOptions) {
    //       this.filter.sortOptions.isAscending = this.isAscendingSort;
    //       this.filter.sortOptions.sortField = this.filterSettings.presets.sortOptions.sortField;
    //     }
    //   }
    //
    //   if (this.filterSettings.presets.rating > 0) {
    //     this.updateRating(this.filterSettings.presets.rating);
    //   }
    //
    //   if (this.filterSettings.presets.seriesNameQuery !== '') {
    //     this.seriesNameGroup.get('searchNameQuery')?.setValue(this.filterSettings.presets.seriesNameQuery);
    //   }
    // }

    this.filterV2 = this.filterSettings.presetsV2;
    console.log('filterV2: ', this.filterV2);

    this.fullyLoaded = true;
    this.cdRef.markForCheck();
    this.apply();
  }



  updateRating(rating: any) {
    if (this.filterSettings.ratingDisabled) return;
    this.filter.rating = rating;
  }

  updateSortOrder() {
    if (this.filterSettings.sortDisabled) return;
    this.isAscendingSort = !this.isAscendingSort;
    if (this.filter.sortOptions === null) {
      this.filter.sortOptions = {
        isAscending: this.isAscendingSort,
        sortField: SortField.SortName
      }
    }

    this.filter.sortOptions.isAscending = this.isAscendingSort;
  }

  clear() {
    this.filter = this.filterUtilityService.createSeriesFilter();
    this.readProgressGroup.get('read')?.setValue(true);
    this.readProgressGroup.get('notRead')?.setValue(true);
    this.readProgressGroup.get('inProgress')?.setValue(true);
    this.sortGroup.get('sortField')?.setValue(SortField.SortName);
    this.isAscendingSort = true;
    this.seriesNameGroup.get('seriesNameQuery')?.setValue('');
    this.cdRef.markForCheck();
    // Apply any presets which will trigger the apply
    this.loadFromPresetsAndSetup();
  }

  apply() {

    this.applyFilter.emit({filter: this.filter, isFirst: this.updateApplied === 0, filterV2: this.filterV2!});

    if (this.utilityService.getActiveBreakpoint() === Breakpoint.Mobile && this.updateApplied !== 0) {
      this.toggleSelected();
    }

    this.updateApplied++;
    this.cdRef.markForCheck();
  }

  toggleSelected() {
    this.toggleService.toggle();
    this.cdRef.markForCheck();
  }

  setToggle(event: any) {
    this.toggleService.set(!this.filteringCollapsed);
  }

}
