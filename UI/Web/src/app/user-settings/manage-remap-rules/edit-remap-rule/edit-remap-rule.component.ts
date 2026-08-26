import {ChangeDetectionStrategy, Component, inject, input, OnInit, output, signal} from '@angular/core';
import {NonNullableFormBuilder, ReactiveFormsModule} from '@angular/forms';
import {ReplaySubject} from 'rxjs';
import {TranslocoDirective} from '@jsverse/transloco';
import {CblService} from '../../../_services/cbl.service';
import {SearchService} from '../../../_services/search.service';
import {ImageService} from '../../../_services/image.service';
import {RemapRule} from '../../../_models/reading-list/cbl/remap-rule';
import {SearchResult} from '../../../_models/search/search-result';
import {Chapter} from '../../../_models/chapter';
import {TypeaheadSettings} from '../../../typeahead/_models/typeahead-settings';
import {TypeaheadComponent} from '../../../typeahead/_components/typeahead.component';
import {ImageComponent} from '../../../shared/image/image.component';
import {TypeaheadSettingsFactoryService} from "../../../typeahead-settings-factory.service";
import {FormFieldDirective} from "../../../_directives/form-field.directive";

@Component({
  selector: 'app-edit-remap-rule',
  templateUrl: './edit-remap-rule.component.html',
  styleUrls: ['./edit-remap-rule.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, ReactiveFormsModule, TypeaheadComponent, ImageComponent, FormFieldDirective]
})
export class EditRemapRuleComponent implements OnInit {

  private readonly fb = inject(NonNullableFormBuilder);
  private readonly cblService = inject(CblService);
  private readonly searchService = inject(SearchService);
  private readonly typeaheadSettingsFactory = inject(TypeaheadSettingsFactoryService);
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

  seriesSettings = signal<TypeaheadSettings<SearchResult> | null>(null);
  seriesReset = new ReplaySubject<boolean>(1);
  chapterReset = new ReplaySubject<boolean>(1);

  ngOnInit() {
    const editRule = this.rule();
    this.seriesSettings.set(this.createSeriesTypeahead(editRule));

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
    const savedData = editRule ? {seriesId: editRule.seriesId, name: editRule.seriesNameAtMapping} as SearchResult : undefined;
    return this.typeaheadSettingsFactory.forSearchResult({id: 'remap-series', savedData});
  }

  private createChapterTypeahead(seriesId: number): TypeaheadSettings<Chapter> {
    return this.typeaheadSettingsFactory.forChapter({id: `remap-chapter-${seriesId}`, seriesId});
  }
}
