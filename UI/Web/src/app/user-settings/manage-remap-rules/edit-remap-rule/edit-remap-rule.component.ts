import {ChangeDetectionStrategy, Component, inject, input, OnInit, output, signal} from '@angular/core';
import {NonNullableFormBuilder, ReactiveFormsModule} from '@angular/forms';
import {ReplaySubject} from 'rxjs';
import {map} from 'rxjs/operators';
import {TranslocoDirective} from '@jsverse/transloco';
import {CblService} from '../../../_services/cbl.service';
import {SearchService} from '../../../_services/search.service';
import {UtilityService} from '../../../shared/_services/utility.service';
import {ImageService} from '../../../_services/image.service';
import {RemapRule} from '../../../_models/reading-list/cbl/remap-rule';
import {SearchResult} from '../../../_models/search/search-result';
import {Chapter} from '../../../_models/chapter';
import {TypeaheadSettings} from '../../../typeahead/_models/typeahead-settings';
import {TypeaheadComponent} from '../../../typeahead/_components/typeahead.component';
import {ImageComponent} from '../../../shared/image/image.component';

@Component({
  selector: 'app-edit-remap-rule',
  templateUrl: './edit-remap-rule.component.html',
  styleUrls: ['./edit-remap-rule.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, ReactiveFormsModule, TypeaheadComponent, ImageComponent]
})
export class EditRemapRuleComponent implements OnInit {

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly cblService = inject(CblService);
  private readonly searchService = inject(SearchService);
  private readonly utilityService = inject(UtilityService);
  protected readonly imageService = inject(ImageService);

  rule = input<RemapRule | null>(null);
  saved = output<RemapRule>();
  cancelled = output<void>();

  form = this.fb.group({
    cblSeriesName: '',
    cblVolume: '',
    cblNumber: '',
  });

  selectedSeries = signal<SearchResult | null>(null);
  selectedChapter = signal<Chapter | null>(null);
  chapterSettings = signal<TypeaheadSettings<Chapter> | null>(null);

  seriesSettings!: TypeaheadSettings<SearchResult>;
  seriesReset = new ReplaySubject<boolean>(1);
  chapterReset = new ReplaySubject<boolean>(1);

  ngOnInit() {
    const editRule = this.rule();
    this.seriesSettings = this.createSeriesTypeahead(editRule);

    if (editRule) {
      this.form.patchValue({
        cblSeriesName: editRule.cblSeriesName,
        cblVolume: editRule.cblVolume ?? '',
        cblNumber: editRule.cblNumber ?? '',
      });

      const seriesStub = {seriesId: editRule.seriesId, name: editRule.seriesNameAtMapping} as SearchResult;
      this.selectedSeries.set(seriesStub);

      const chapterTypeahead = this.createChapterTypeahead(editRule.seriesId);

      if (editRule.chapterId) {
        const chapterStub = this.cblService.buildChapterStub(editRule);
        chapterStub.id = editRule.chapterId;
        chapterStub.volumeId = editRule.volumeId ?? 0;
        chapterStub.title = editRule.chapterRange;
        chapterTypeahead.savedData = chapterStub;
        this.selectedChapter.set(chapterStub);
      }

      this.chapterSettings.set(chapterTypeahead);
    }
  }

  onSeriesSelected(event: SearchResult[]) {
    const series = event.length > 0 ? event[0] : null;
    this.selectedSeries.set(series);
    this.selectedChapter.set(null);

    if (series) {
      this.chapterSettings.set(this.createChapterTypeahead(series.seriesId));
    } else {
      this.chapterSettings.set(null);
    }
  }

  onChapterSelected(event: Chapter[]) {
    this.selectedChapter.set(event.length > 0 ? event[0] : null);
  }

  cancel() {
    this.cancelled.emit();
  }

  save() {
    const {cblSeriesName, cblVolume, cblNumber} = this.form.value;
    const selectedSeries = this.selectedSeries();
    if (!cblSeriesName?.trim() || !selectedSeries) return;

    const chapter = this.selectedChapter();
    const issueDetail: {cblVolume?: string; cblNumber?: string; volumeId?: number; chapterId?: number} = {};

    if (cblVolume?.trim()) issueDetail.cblVolume = cblVolume.trim();
    if (cblNumber?.trim()) issueDetail.cblNumber = cblNumber.trim();
    if (chapter) {
      issueDetail.volumeId = chapter.volumeId;
      issueDetail.chapterId = chapter.id;
    }

    const existingRule = this.rule();
    const obs$ = existingRule
      ? this.cblService.updateRemapRule(existingRule.id, {
          cblSeriesName: cblSeriesName.trim(),
          seriesId: selectedSeries.seriesId,
          ...issueDetail,
        })
      : this.cblService.createRemapRule(
          cblSeriesName.trim(),
          selectedSeries.seriesId,
          Object.keys(issueDetail).length > 0 ? issueDetail : undefined
        );

    obs$.subscribe(rule => {
      this.saved.emit(rule);
    });
  }

  private createSeriesTypeahead(editRule: RemapRule | null): TypeaheadSettings<SearchResult> {
    const settings = new TypeaheadSettings<SearchResult>();
    settings.minCharacters = 2;
    settings.multiple = false;
    settings.id = 'remap-series';
    settings.unique = true;
    settings.addIfNonExisting = false;
    settings.fetchFn = (filter: string) =>
      this.searchService.search(filter).pipe(
        map(group => group.series),
        map(items => settings.compareFn(items, filter)),
      );
    settings.trackByIdentityFn = (_idx, item) => item.seriesId + '';
    settings.compareFn = (options: SearchResult[], filter: string) => {
      return options.filter(m => {
        return this.utilityService.filter(m.name, filter) || this.utilityService.filter(m.localizedName, filter);
      });
    };
    settings.selectionCompareFn = (a: SearchResult, b: SearchResult) => {
      return a.seriesId === b.seriesId;
    };

    if (editRule) {
      settings.savedData = {seriesId: editRule.seriesId, name: editRule.seriesNameAtMapping} as SearchResult;
    }

    return settings;
  }

  private createChapterTypeahead(seriesId: number): TypeaheadSettings<Chapter> {
    const settings = new TypeaheadSettings<Chapter>();
    settings.minCharacters = 0;
    settings.multiple = false;
    settings.id = 'remap-chapter-' + seriesId;
    settings.unique = true;
    settings.addIfNonExisting = false;
    settings.fetchFn = (searchFilter: string) => this.searchService.getChaptersBySeries(seriesId).pipe(
      map(chapters => {
        if (!searchFilter) return chapters;
        const lower = searchFilter.toLowerCase().trim();
        return chapters.filter(c =>
          c.title?.toLowerCase().includes(lower) ||
          c.range?.toLowerCase().includes(lower) ||
          c.titleName?.toLowerCase().includes(lower)
        );
      })
    );
    settings.trackByIdentityFn = (_idx, item) => item.id + '';
    settings.compareFn = (options: Chapter[], filter: string) => {
      if (!filter) return options;
      const lower = filter.toLowerCase().trim();
      return options.filter(c =>
        c.title?.toLowerCase().includes(lower) ||
        c.range?.toLowerCase().includes(lower) ||
        c.titleName?.toLowerCase().includes(lower)
      );
    };
    settings.selectionCompareFn = (a: Chapter, b: Chapter) => {
      return a.id === b.id;
    };

    return settings;
  }
}
