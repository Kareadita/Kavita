import {ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal} from '@angular/core';
import {NgbActiveModal, NgbModal, NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {CblSavedFile} from '../../../_models/reading-list/cbl/cbl-saved-file';
import {CblImportSummary} from '../../../_models/reading-list/cbl/cbl-import-summary';
import {CblBookResult} from '../../../_models/reading-list/cbl/cbl-book-result';
import {CblImportReason} from '../../../_models/reading-list/cbl/cbl-import-reason.enum';
import {CblMatchTier} from '../../../_models/reading-list/cbl/cbl-match-tier';
import {CblImportDecisions} from '../../../_models/reading-list/cbl/cbl-import-decisions';
import {RemapRule} from '../../../_models/reading-list/cbl/remap-rule';
import {CblSeriesCandidate} from '../../../_models/reading-list/cbl/cbl-series-candidate';
import {Chapter} from '../../../_models/chapter';
import {CblService} from '../../../_services/cbl.service';
import {SearchService} from '../../../_services/search.service';
import {ToastrService} from 'ngx-toastr';
import {TypeaheadSettings} from '../../../typeahead/_models/typeahead-settings';
import {SearchResult} from '../../../_models/search/search-result';
import {UtilityService} from '../../../shared/_services/utility.service';
import {TypeaheadComponent} from '../../../typeahead/_components/typeahead.component';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {CblMatchTierPipe} from '../../../_pipes/cbl-match-tier.pipe';
import {CblImportReasonPipe} from '../../../_pipes/cbl-import-reason.pipe';
import {ManageRemapRulesModalComponent} from '../manage-remap-rules-modal/manage-remap-rules-modal.component';
import {ImageComponent} from '../../../shared/image/image.component';
import {ImageService} from '../../../_services/image.service';
import {map} from 'rxjs';
import {LibraryService} from '../../../_services/library.service';
import {
  DataTableColumnCellDirective,
  DataTableColumnDirective,
  DataTableColumnHeaderDirective,
  DatatableComponent,
} from '@siemens/ngx-datatable';
import {CdkScrollable} from '@angular/cdk/scrolling';
import {RouterLink} from '@angular/router';
import {EntityTitleComponent} from '../../../cards/entity-title/entity-title.component';
import {modalSaved} from "../../../_models/modal/modal-result";
import {WikiLink} from "../../../_models/wiki";

export interface CblIssueRow {
  result: CblBookResult;
  remapRuleId: number | null;
  skipped: boolean;
}

@Component({
  selector: 'app-import-cbl-modal',
  imports: [
    TranslocoDirective,
    TypeaheadComponent,
    LoadingComponent,
    CblMatchTierPipe,
    CblImportReasonPipe,
    ImageComponent,
    DatatableComponent,
    DataTableColumnDirective,
    DataTableColumnCellDirective,
    DataTableColumnHeaderDirective,
    NgbTooltip,
    CdkScrollable,
    RouterLink,
    EntityTitleComponent,
  ],
  templateUrl: './import-cbl-modal.component.html',
  styleUrl: './import-cbl-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImportCblModalComponent implements OnInit {
  private readonly modal = inject(NgbActiveModal);
  private readonly modalService = inject(NgbModal);
  private readonly cblService = inject(CblService);
  private readonly searchService = inject(SearchService);
  private readonly toastr = inject(ToastrService);
  private readonly utilityService = inject(UtilityService);
  private readonly libraryService = inject(LibraryService);
  protected readonly imageService = inject(ImageService);

  savedFiles = input.required<CblSavedFile[]>();

  currentFileIndex = signal(0);
  currentFile = computed(() => this.savedFiles()[this.currentFileIndex()]);
  currentSummary = signal<CblImportSummary | null>(null);
  isProcessing = signal(false);
  remapRules = signal<RemapRule[]>([]);

  /** All rows (matched + issues) for the unified table */
  allRows = signal<CblIssueRow[]>([]);
  classifiedRows = computed(() =>
    this.allRows().map(r => ({
      ...r,
      category: this.classifyRow(r)
    }))
  );
  libraryNames = signal<Record<number, string>>({});

  showMatched = signal(true);
  showIssues = signal(true);
  showUnmatched = signal(true);
  visibleRows = computed(() => {
    const active = new Set<string>();
    if (this.showMatched()) active.add('matched');
    if (this.showIssues()) active.add('issue');
    if (this.showUnmatched()) active.add('unmatched');

    if (active.size === 0) return [];
    if (active.size === 3) return this.classifiedRows();

    return this.classifiedRows().filter(r => active.has(r.category));
  });

  matchedCount = computed(() => this.classifiedRows().filter(r => r.category === 'matched').length);
  issueCount = computed(() => this.classifiedRows().filter(r => r.category === 'issue').length);
  unmatchedCount = computed(() => this.classifiedRows().filter(r => r.category === 'unmatched').length);

  /** Lazy typeahead state, only one row can be resolving at a time */
  activeRow = signal<CblIssueRow | null>(null);
  activeSeriesTypeahead = signal<TypeaheadSettings<SearchResult> | null>(null);
  activeChapterTypeahead = signal<TypeaheadSettings<Chapter> | null>(null);

  /** Track the CBL series name of the row being resolved, so we can auto-continue after re-validation */
  private pendingAutoEditSeries: string | null = null;

  private classifyRow(r: CblIssueRow): 'matched' | 'issue' | 'unmatched' {
    if (r.result.reason === CblImportReason.Success) return 'matched';
    if (r.skipped) return 'matched';
    if (r.result.matchTier === CblMatchTier.Unmatched) return 'unmatched';
    return 'issue';
  }

  getRowClass = (row: CblIssueRow) => {
    if (row.skipped) return 'skipped-row';
    if (row.result.reason === CblImportReason.Success) return 'matched-row';
    return 'issue-row';
  };

  getMatchBadgeClass(matchTier: CblMatchTier) {
    switch (matchTier) {
      case CblMatchTier.RemapRule:
      case CblMatchTier.ExternalId:
      case CblMatchTier.ExactName:
      case CblMatchTier.ComicVineNaming:
      case CblMatchTier.ArticleStripped:
      case CblMatchTier.ReprintStripped:
      case CblMatchTier.AlternateSeries:
        return 'success';
      case CblMatchTier.UserDecision:
        return 'warning';
      case CblMatchTier.Unmatched:
        return 'danger';

    }
  }

  ngOnInit() {
    this.cblService.getRemapRules().subscribe(rules => {
      this.remapRules.set(rules);
      this.validateCurrentFile();
    });

    this.libraryService.getLibraryNames().subscribe(names => {
      this.libraryNames.set(names);
    });
  }

  dismiss() {
    this.modal.dismiss();
  }

  previousFile() {
    if (this.currentFileIndex() > 0) {
      this.currentFileIndex.set(this.currentFileIndex() - 1);
      this.validateCurrentFile();
    }
  }

  nextFile() {
    if (this.currentFileIndex() < this.savedFiles().length - 1) {
      this.currentFileIndex.set(this.currentFileIndex() + 1);
      this.validateCurrentFile();
    }
  }

  validateCurrentFile() {
    const file = this.currentFile();
    if (!file) return;

    this.isProcessing.set(true);
    this.cancelResolve();
    this.cblService.reValidate(file.fileName).subscribe({
      next: (summary) => {
        this.currentSummary.set(summary);
        this.buildAllRows(summary);
        this.isProcessing.set(false);

        // Auto-continue: if a pending series was just resolved and is now chapter-missing, auto-edit
        if (this.pendingAutoEditSeries) {
          const seriesName = this.pendingAutoEditSeries;
          this.pendingAutoEditSeries = null;
          const row = this.allRows().find(r =>
            r.result.series === seriesName && this.isChapterMissing(r)
          );
          if (row) {
            this.startResolve(row);
          }
        }
      },
      error: () => {
        this.toastr.error(translate('toasts.failed-to-validate'));
        this.isProcessing.set(false);
        this.pendingAutoEditSeries = null;
      }
    });
  }

  isSeriesMissing(row: CblIssueRow): boolean {
    return row.result.reason === CblImportReason.SeriesMissing ||
      row.result.reason === CblImportReason.AllSeriesMissing;
  }

  isChapterMissing(row: CblIssueRow): boolean {
    return row.result.reason === CblImportReason.ChapterMissing ||
      row.result.reason === CblImportReason.VolumeMissing;
  }

  isSeriesCollision(row: CblIssueRow): boolean {
    return row.result.reason === CblImportReason.SeriesCollision;
  }

  /** Whether this row is the active editing row showing a series typeahead */
  isEditingSeries(row: CblIssueRow): boolean {
    return this.activeRow()?.result.order === row.result.order && this.activeSeriesTypeahead() !== null;
  }

  /** Whether this row is the active editing row showing a chapter typeahead */
  isEditingChapter(row: CblIssueRow): boolean {
    return this.activeRow()?.result.order === row.result.order && this.activeChapterTypeahead() !== null;
  }

  /** Build a minimal Chapter stub for entity-title rendering */
  buildChapterStub(result: CblBookResult): Chapter {
    return {
      volumeId: 0,
      range: result.chapterNumber || result.chapterTitle,
      titleName: result.chapterTitle !== result.chapterNumber ? result.chapterTitle : '',
      isSpecial: false,
    } as Chapter;
  }

  /** Auto-detect which typeahead to open based on row reason */
  startResolve(row: CblIssueRow) {
    if (this.isSeriesMissing(row) || this.isSeriesCollision(row) || row.result.reason === CblImportReason.Success) {
      this.startResolveSeries(row);
    } else if (this.isChapterMissing(row)) {
      this.startResolveChapter(row);
    }
  }

  /** Explicitly open the series typeahead for a row */
  startResolveSeries(row: CblIssueRow) {
    if (this.activeRow() === row && this.activeSeriesTypeahead() !== null) {
      this.cancelResolve();
      return;
    }
    this.clearActiveState();
    this.activeRow.set(row);
    this.activeSeriesTypeahead.set(this.createSeriesTypeahead(row.result));
    this.allRows.set([...this.allRows()]);
  }

  /** Explicitly open the chapter typeahead for a row */
  startResolveChapter(row: CblIssueRow) {
    if (this.activeRow() === row && this.activeChapterTypeahead() !== null) {
      this.cancelResolve();
      return;
    }
    this.clearActiveState();
    this.activeRow.set(row);
    this.activeChapterTypeahead.set(
      row.result.seriesId > 0 ? this.createChapterTypeahead(row.result.seriesId) : null
    );
    this.allRows.set([...this.allRows()]);
  }

  private clearActiveState() {
    this.activeRow.set(null);
    this.activeSeriesTypeahead.set(null);
    this.activeChapterTypeahead.set(null);
  }

  cancelResolve() {
    this.activeRow.set(null);
    this.activeSeriesTypeahead.set(null);
    this.activeChapterTypeahead.set(null);
    this.allRows.set([...this.allRows()]);
  }

  onCandidateSelected(row: CblIssueRow, candidate: CblSeriesCandidate) {
    this.handleSeriesSelection(row, candidate.seriesId);
  }

  onSeriesTypeaheadSelected(row: CblIssueRow, event: SearchResult[]) {
    if (!event || event.length === 0) return;
    const selected = event[0];

    // If editing a matched row and the user picked the same series, just cancel
    if (row.result.reason === CblImportReason.Success && selected.seriesId === row.result.seriesId) {
      this.cancelResolve();
      return;
    }

    this.handleSeriesSelection(row, selected.seriesId);
  }

  onChapterTypeaheadSelected(row: CblIssueRow, event: Chapter[]) {
    if (!event || event.length === 0) return;
    const chapter = event[0];
    this.handleChapterSelection(row, chapter);
  }

  getRemapRuleTooltip(row: CblIssueRow): string {
    if (row.result.matchTier !== CblMatchTier.RemapRule) return '';
    const rule = this.remapRules().find(r =>
      r.normalizedCblSeriesName === row.result.series.toLowerCase().replace(/[^a-z0-9]/g, '')
      || r.cblSeriesName === row.result.series
    );
    if (!rule) return translate('import-cbl-modal.remap-rule-used');
    return `${rule.cblSeriesName || rule.normalizedCblSeriesName} → ${rule.seriesNameAtMapping}`;
  }

  toggleSkip(row: CblIssueRow) {
    if (this.activeRow() === row) {
      this.cancelResolve();
    }
    row.skipped = !row.skipped;
    this.allRows.set([...this.allRows()]);
  }

  toggleRowFilter(category: 'matched' | 'issues' | 'unmatched') {
    switch (category) {
      case 'matched': this.showMatched.update(v => !v); break;
      case 'issues': this.showIssues.update(v => !v); break;
      case 'unmatched': this.showUnmatched.update(v => !v); break;
    }
  }



  openRemapRulesModal() {
    const ref = this.modalService.open(ManageRemapRulesModalComponent, {size: 'lg'});
    ref.closed.subscribe((hasModifications: boolean) => {
      if (hasModifications) {
        this.cblService.getRemapRules().subscribe(rules => {
          this.remapRules.set([...rules]);
          this.validateCurrentFile();
        });
      }
    });
  }

  async finalizeAll() {
    this.isProcessing.set(true);

    for (let i = 0; i < this.savedFiles().length; i++) {
      const file = this.savedFiles()[i];

      if (i !== this.currentFileIndex()) {
        this.currentFileIndex.set(i);
      }

      const decisions: CblImportDecisions = {
        itemResolutions: {},
        saveAsRemapRules: false
      };

      const repoMeta = file.repoPath ? {
        repoPath: file.repoPath,
        downloadUrl: file.downloadUrl!,
        sha: file.sha!
      } : file.downloadUrl ? {
        repoPath: '',
        downloadUrl: file.downloadUrl,
        sha: ''
      } : undefined;

      try {
        await this.cblService.finalizeImport(file.fileName, decisions, file.provider, repoMeta).toPromise();
      } catch {
        this.toastr.error(translate('toasts.failed-to-import', {name: file.name}));
      }
    }

    this.isProcessing.set(false);
    this.toastr.success(translate('toasts.import-complete'));
    this.modal.close(modalSaved(true));
  }

  private buildAllRows(summary: CblImportSummary) {
    const allResults = [
      ...(summary.successfulInserts || []),
      ...(summary.results || [])
    ].sort((a, b) => a.order - b.order);

    const rows: CblIssueRow[] = allResults.map(result => ({
      result,
      remapRuleId: null,
      skipped: false,
    }));

    this.allRows.set(rows);
  }

  private async handleSeriesSelection(row: CblIssueRow, seriesId: number) {
    // Remember this series for auto-continue after re-validation
    this.pendingAutoEditSeries = row.result.series;

    this.cblService.createRemapRule(row.result.series, seriesId, {
      cblVolume: row.result.volume || undefined, // Pass the volume if it's available to ensure volume-level mapping works
    }).subscribe(rule => {
      row.remapRuleId = rule.id;

      // The backend might have updated the ruleset, so refresh them
      this.cblService.getRemapRules().subscribe(rules => {
        this.remapRules.set([...rules]);
        this.cancelResolve();
        this.validateCurrentFile();
      });
    });
  }

  private handleChapterSelection(row: CblIssueRow, chapter: Chapter) {
    this.cblService.createRemapRule(row.result.series, row.result.seriesId, {
      cblVolume: row.result.volume || undefined,
      cblNumber: row.result.number || undefined,
      volumeId: chapter.volumeId,
      chapterId: chapter.id,
    }).subscribe(rule => {
      row.remapRuleId = rule.id;

      this.cblService.getRemapRules().subscribe(rules => {
        this.remapRules.set([...rules]);
        this.cancelResolve();
        this.validateCurrentFile();
      });
    });
  }

  private createSeriesTypeahead(result: CblBookResult): TypeaheadSettings<SearchResult> {
    const settings = new TypeaheadSettings<SearchResult>();
    settings.minCharacters = 0;
    settings.multiple = false;
    settings.id = 'cbl-series-' + result.order;
    settings.unique = true;
    settings.addIfNonExisting = false;
    settings.fetchFn = (searchFilter: string) => this.searchService.search(searchFilter).pipe(
      map(group => group.series),
      map(items => settings.compareFn(items, searchFilter))
    );
    settings.trackByIdentityFn = (idx, item) => item.seriesId + '';
    settings.compareFn = (options: SearchResult[], filter: string) => {
      return options.filter(m => {
        return this.utilityService.filter(m.name, filter) || this.utilityService.filter(m.localizedName, filter);
      });
    };
    settings.selectionCompareFn = (a: SearchResult, b: SearchResult) => {
      return a.seriesId === b.seriesId;
    };
    settings.dropdownPosition = 'body';

    return settings;
  }

  private createChapterTypeahead(seriesId: number): TypeaheadSettings<Chapter> {
    const settings = new TypeaheadSettings<Chapter>();
    settings.minCharacters = 0;
    settings.multiple = false;
    settings.id = 'cbl-chapter-' + seriesId;
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
    settings.trackByIdentityFn = (idx, item) => item.id + '';
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
    settings.dropdownPosition = 'body';

    return settings;
  }

  protected readonly CblImportReason = CblImportReason;
  protected readonly CblMatchTier = CblMatchTier;
  protected readonly WikiLink = WikiLink;
}
