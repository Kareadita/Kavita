import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  input,
  OnInit,
  output,
  signal
} from '@angular/core';
import {FormControl, ReactiveFormsModule} from '@angular/forms';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TypeaheadComponent} from "../../typeahead/_components/typeahead.component";
import {TranslocoModule} from "@jsverse/transloco";
import {RelationshipPipe} from "../../_pipes/relationship.pipe";
import {WikiLink} from "../../_models/wiki";
import {TypeaheadSettings} from "../../typeahead/_models/typeahead-settings";
import {SeriesService} from "../../_services/series.service";
import {LibraryService} from "../../_services/library.service";
import {ImageService} from "../../_services/image.service";
import {RelationKind, RelationKinds} from "../../_models/series-detail/relation-kind";
import {SearchResult} from "../../_models/search/search-result";
import {Series} from "../../_models/series";
import {TypeaheadSettingsFactoryService} from "../../typeahead-settings-factory.service";

interface RelationControl {
  series: {id: number, name: string} | undefined; // Will add type as well
  typeaheadSettings: TypeaheadSettings<SearchResult>;
  formControl: FormControl;
  id: number; // Random id used track by
}

@Component({
    selector: 'app-edit-series-relation',
    imports: [
        TypeaheadComponent,
        ReactiveFormsModule,
        TranslocoModule,
        RelationshipPipe,
    ],
    templateUrl: './edit-series-relation.component.html',
    styleUrls: ['./edit-series-relation.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditSeriesRelationComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly seriesService = inject(SeriesService);
  private readonly libraryService = inject(LibraryService);
  private readonly typeaheadSettingsFactory = inject(TypeaheadSettingsFactoryService);
  public readonly imageService = inject(ImageService);

  readonly series = input.required<Series>();
  readonly save = input<EventEmitter<void>>(new EventEmitter());
  readonly saveApi = output();

  relationOptions = RelationKinds;
  relations = signal<RelationControl[]>([]);
  libraryNames = signal<Record<number, string>>({});

  focusTypeahead = new EventEmitter();

  idCount = 0;

  ngOnInit(): void {
    this.seriesService.getRelatedForSeries(this.series().id).subscribe(relations => {
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
        this.setupRelationRows(relations.editions, RelationKind.Edition);
        this.setupRelationRows(relations.annuals, RelationKind.Annual);
        this.setupRelationRows(relations.cameos, RelationKind.Cameo);
    });

    this.libraryService.getLibraryNames().subscribe(names => {
      this.libraryNames.set(names);
    });

    this.save().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.saveState());
  }

  setupRelationRows(relations: Array<Series>, kind: RelationKind) {
    for (let indx = 0; indx < relations.length; indx++) {
      const item = relations[indx];
      const settings = this.createSeriesTypeahead(item, indx);
      const form = new FormControl(kind, []);
      if (kind === RelationKind.Parent) {
        form.disable();
      }

      const relation = {series: item, typeaheadSettings: settings, formControl: form, id: this.idCount++};
      this.relations.set([...this.relations(), relation]);
    }

  }

  async addNewRelation() {
    this.relations.set([...this.relations(), {
      series: undefined,
      formControl: new FormControl(RelationKind.Adaptation, []),
      typeaheadSettings: this.createSeriesTypeahead(undefined, this.relations().length),
      id: this.idCount++,
    }]);

    // Focus on the new typeahead
    setTimeout(() => {
      this.focusTypeahead.emit(`relation--${this.relations().length - 1}`);
    }, 10);
  }

  removeRelation(index: number) {
    const relations = this.relations();
    relations.splice(index, 1);
    this.relations.set([...relations]);
  }


  updateSeries(event: Array<SearchResult | undefined>, relation: RelationControl) {
    if (event[0] === undefined) {
      relation.series = undefined;
      return;
    }
    relation.series = {id: event[0].seriesId, name: event[0].name};
  }

  createSeriesTypeahead(series: Series | undefined, index: number): TypeaheadSettings<SearchResult> {

    const savedData = series ? {
      name: series.name,
      libraryId: series.libraryId,
      libraryName: series.localizedName,
      seriesId: series.id,
      format: series.format,
      localizedName: series.localizedName,
      originalName: series.originalName,
      sortName: series.sortName,
    } : undefined;

    return this.typeaheadSettingsFactory.forSearchResult({id: `relation--${index}`,
      excludeSeriesId: this.series().id,
      savedData,
      overrides: {
        minCharacters: 0,
      }
    });

  }

  saveState() {
    const g = (kind: RelationKind) => this.getSeriesIdsForRelation(kind);

    this.seriesService.updateRelationships(
      this.series().id,
      g(RelationKind.Adaptation),
      g(RelationKind.Character),
      g(RelationKind.Contains),
      g(RelationKind.Other),
      g(RelationKind.Prequel),
      g(RelationKind.Sequel),
      g(RelationKind.SideStory),
      g(RelationKind.SpinOff),
      g(RelationKind.AlternativeSetting),
      g(RelationKind.AlternativeVersion),
      g(RelationKind.Doujinshi),
      g(RelationKind.Edition),
      g(RelationKind.Annual),
      g(RelationKind.Cameo)
    ).subscribe(() => {}); // NOTE: We can actually emit this onto an observable and in main parent, use mergeMap into the forkJoin
  }

  private getSeriesIdsForRelation(relation: RelationKind) {
    return this.relations().filter(item => (parseInt(item.formControl.value, 10) as RelationKind) === relation && item.series !== undefined).map(item => item.series!.id)
  }

  protected readonly RelationKind = RelationKind;
  protected readonly WikiLink = WikiLink;
}
