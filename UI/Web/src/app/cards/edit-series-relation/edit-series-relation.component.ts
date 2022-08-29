import { ChangeDetectionStrategy, ChangeDetectorRef, Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { FormControl } from '@angular/forms';
import { map, Subject, Observable, of, firstValueFrom, takeUntil, ReplaySubject } from 'rxjs';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { TypeaheadSettings } from 'src/app/typeahead/typeahead-settings';
import { SearchResult } from 'src/app/_models/search-result';
import { Series } from 'src/app/_models/series';
import { RelationKind, RelationKinds } from 'src/app/_models/series-detail/relation-kind';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';
import { SeriesService } from 'src/app/_services/series.service';

interface RelationControl {
  series: {id: number, name: string} | undefined; // Will add type as well
  typeaheadSettings: TypeaheadSettings<SearchResult>;
  formControl: FormControl;
}

@Component({
  selector: 'app-edit-series-relation',
  templateUrl: './edit-series-relation.component.html',
  styleUrls: ['./edit-series-relation.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditSeriesRelationComponent implements OnInit, OnDestroy {

  @Input() series!: Series;
  /**
   * This will tell the component to save based on it's internal state
   */
  @Input() save: EventEmitter<void> = new EventEmitter();

  @Output() saveApi = new ReplaySubject(1);

  relationOptions = RelationKinds;
  relations: Array<RelationControl> = [];
  seriesSettings: TypeaheadSettings<SearchResult> = new TypeaheadSettings();
  libraryNames: {[key:number]: string} = {};

  focusTypeahead = new EventEmitter();

  get RelationKind() {
    return RelationKind;
  }


  private onDestroy: Subject<void> = new Subject<void>();

  constructor(private seriesService: SeriesService, private utilityService: UtilityService, 
    public imageService: ImageService, private libraryService: LibraryService, 
    private readonly cdRef: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.seriesService.getRelatedForSeries(this.series.id).subscribe(async relations => {
        this.setupRelationRows(relations.prequels, RelationKind.Prequel);
        this.setupRelationRows(relations.sequels, RelationKind.Sequel);
        this.setupRelationRows(relations.sideStories, RelationKind.SideStory);
        this.setupRelationRows(relations.spinOffs, RelationKind.SpinOff);
        this.setupRelationRows(relations.adaptations, RelationKind.Adaptation);
        this.setupRelationRows(relations.others, RelationKind.Other);
        this.setupRelationRows(relations.characters, RelationKind.Character);
        this.setupRelationRows(relations.alternativeSettings, RelationKind.AlternativeSetting);
        this.setupRelationRows(relations.alternativeVersions, RelationKind.AlternativeVersion);
        this.setupRelationRows(relations.doujinshis, RelationKind.Doujinshi);
        this.setupRelationRows(relations.contains, RelationKind.Contains);
        this.setupRelationRows(relations.parent, RelationKind.Parent);
        this.cdRef.detectChanges();
    });

    this.libraryService.getLibraryNames().subscribe(names => {
      this.libraryNames = names;
      this.cdRef.markForCheck();
    });

    this.save.pipe(takeUntil(this.onDestroy)).subscribe(() => this.saveState());
  }

  ngOnDestroy(): void {
      this.onDestroy.next();
      this.onDestroy.complete();
  }

  setupRelationRows(relations: Array<Series>, kind: RelationKind) {
    relations.map(async (item, indx) => {
      const settings = await firstValueFrom(this.createSeriesTypeahead(item, kind, indx));
      const form = new FormControl(kind, []);
      if (kind === RelationKind.Parent) {
        form.disable();
      }
      return {series: item, typeaheadSettings: settings, formControl: form};
    }).forEach(async p => {
      this.relations.push(await p);
      this.cdRef.markForCheck();
    });
  }

  async addNewRelation() {
    this.relations.push({series: undefined, formControl: new FormControl(RelationKind.Adaptation, []), typeaheadSettings: await firstValueFrom(this.createSeriesTypeahead(undefined, RelationKind.Adaptation, this.relations.length))});
    this.cdRef.markForCheck();

    // Focus on the new typeahead
    setTimeout(() => {
      this.focusTypeahead.emit(`relation--${this.relations.length - 1}`);
    }, 10);
  }

  removeRelation(index: number) {
    this.relations.splice(index, 1);
    this.cdRef.markForCheck();
  }

 
  updateSeries(event: Array<SearchResult | undefined>, relation: RelationControl) {
    if (event[0] === undefined) {
      relation.series = undefined;
      this.cdRef.markForCheck();
      return;
    }
    relation.series = {id: event[0].seriesId, name: event[0].name};
    this.cdRef.markForCheck();
  }

  createSeriesTypeahead(series: Series | undefined, relationship: RelationKind, index: number): Observable<TypeaheadSettings<SearchResult>> {
    const seriesSettings = new TypeaheadSettings<SearchResult>();
    seriesSettings.minCharacters = 0;
    seriesSettings.multiple = false;
    seriesSettings.id = 'relation--' + index;
    seriesSettings.unique = true;
    seriesSettings.addIfNonExisting = false;
    seriesSettings.fetchFn = (searchFilter: string) => this.libraryService.search(searchFilter).pipe(
      map(group => group.series),
      map(items => seriesSettings.compareFn(items, searchFilter)),
      map(series => series.filter(s => s.seriesId !== this.series.id)), 
    ); 

    seriesSettings.compareFn = (options: SearchResult[], filter: string) => {
      return options.filter(m => this.utilityService.filter(m.name, filter));
    }

    seriesSettings.selectionCompareFn = (a: SearchResult, b: SearchResult) => {
      return a.seriesId == b.seriesId;
    }

    if (series !== undefined) {
      return this.libraryService.search(series.name).pipe(
        map(group => group.series), map(results => {
          seriesSettings.savedData = results.filter(s => s.seriesId === series.id);
          return seriesSettings;
        }));
    }

    return of(seriesSettings);
  }

  saveState() {
    const adaptations = this.relations.filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === RelationKind.Adaptation && item.series !== undefined).map(item => item.series!.id);
    const characters = this.relations.filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === RelationKind.Character && item.series !== undefined).map(item => item.series!.id);
    const contains = this.relations.filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === RelationKind.Contains && item.series !== undefined).map(item => item.series!.id);
    const others = this.relations.filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === RelationKind.Other && item.series !== undefined).map(item => item.series!.id);
    const prequels = this.relations.filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === RelationKind.Prequel && item.series !== undefined).map(item => item.series!.id);
    const sequels = this.relations.filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === RelationKind.Sequel && item.series !== undefined).map(item => item.series!.id);
    const sideStories = this.relations.filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === RelationKind.SideStory && item.series !== undefined).map(item => item.series!.id);
    const spinOffs = this.relations.filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === RelationKind.SpinOff && item.series !== undefined).map(item => item.series!.id);
    const alternativeSettings = this.relations.filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === RelationKind.AlternativeSetting && item.series !== undefined).map(item => item.series!.id);
    const alternativeVersions = this.relations.filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === RelationKind.AlternativeVersion && item.series !== undefined).map(item => item.series!.id);
    const doujinshis = this.relations.filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === RelationKind.Doujinshi && item.series !== undefined).map(item => item.series!.id);
    
    // NOTE: We can actually emit this onto an observable and in main parent, use mergeMap into the forkJoin
    this.seriesService.updateRelationships(this.series.id, adaptations, characters, contains, others, prequels, sequels, sideStories, spinOffs, alternativeSettings, alternativeVersions, doujinshis).subscribe(() => {});
    
  }

}
