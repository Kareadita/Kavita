import {ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal, viewChild} from '@angular/core';
import {NgbActiveModal, NgbModal} from '@ng-bootstrap/ng-bootstrap';
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
import {ConfirmService} from '../../../shared/confirm.service';
import {ToastrService} from 'ngx-toastr';
import {TypeaheadSettings} from '../../../typeahead/_models/typeahead-settings';
import {SearchResult} from '../../../_models/search/search-result';
import {UtilityService} from '../../../shared/_services/utility.service';
import {TypeaheadComponent} from '../../../typeahead/_components/typeahead.component';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {CblImportResult} from '../../../_models/reading-list/cbl/cbl-import-result.enum';
import {CblMatchTierPipe} from '../../../_pipes/cbl-match-tier.pipe';
import {CblImportReasonPipe} from '../../../_pipes/cbl-import-reason.pipe';
import {ManageRemapRulesModalComponent} from '../manage-remap-rules-modal/manage-remap-rules-modal.component';
import {ImageComponent} from '../../../shared/image/image.component';
import {map} from 'rxjs';
import {LibraryService} from '../../../_services/library.service';
import {
  DataTableColumnCellDirective,
  DataTableColumnDirective,
  DataTableColumnHeaderDirective,
  DatatableComponent,
  DatatableRowDetailDirective,
  DatatableRowDetailTemplateDirective,
} from '@siemens/ngx-datatable';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {CdkScrollable} from '@angular/cdk/scrolling';
import {RouterLink} from '@angular/router';

export interface CblIssueRow {
  result: CblBookResult;
  remapRuleId: number | null;
  skipped: boolean;
  seriesTypeaheadSettings: TypeaheadSettings<SearchResult>;
  chapterTypeaheadSettings: TypeaheadSettings<Chapter> | null;
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
    DatatableRowDetailDirective,
    DatatableRowDetailTemplateDirective,
    NgbTooltip,
    CdkScrollable,
    RouterLink,
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
  private readonly confirmService = inject(ConfirmService);
  private readonly toastr = inject(ToastrService);
  private readonly utilityService = inject(UtilityService);
  private readonly libraryService = inject(LibraryService);

  protected readonly CblImportReason = CblImportReason;
  protected readonly CblImportResult = CblImportResult;

  private readonly table = viewChild(DatatableComponent);

  savedFiles = input.required<CblSavedFile[]>();

  currentFileIndex = signal(0);
  currentFile = computed(() => this.savedFiles()[this.currentFileIndex()]);
  currentSummary = signal<CblImportSummary | null>(null);
  isProcessing = signal(false);
  remapRules = signal<RemapRule[]>([]);

  /** All rows (matched + issues) for the unified table */
  allRows = signal<CblIssueRow[]>([]);
  libraryNames = signal<Record<number, string>>({});

  matchedCount = computed(() => this.allRows().filter(r => r.result.reason === CblImportReason.Success).length);
  issueCount = computed(() => this.allRows().filter(r => r.result.reason !== CblImportReason.Success && !r.skipped).length);

  getRowClass = (row: CblIssueRow) => {
    if (row.skipped) return 'skipped-row';
    if (row.result.reason === CblImportReason.Success) return 'matched-row';
    return 'issue-row';
  };

  ngOnInit() {
    this.cblService.getRemapRules().subscribe(rules => {
      this.remapRules.set(rules);
      this.validateCurrentFile();
    });

    this.libraryService.getLibraryNames().subscribe(names => {
      this.libraryNames.set(names);
    });
  }

  close() {
    this.modal.close();
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
    this.cblService.reValidate(file.fileName).subscribe({
      next: (summary) => {
        this.currentSummary.set(summary);
        this.buildAllRows(summary);
        this.isProcessing.set(false);
      },
      error: () => {
        this.toastr.error(translate('toasts.failed-to-validate'));
        this.isProcessing.set(false);
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

  needsAction(row: CblIssueRow): boolean {
    return row.result.reason !== CblImportReason.Success && !row.skipped;
  }

  onCandidateSelected(row: CblIssueRow, candidate: CblSeriesCandidate) {
    this.handleSeriesSelection(row, candidate.seriesId, candidate.seriesName);
  }

  onSeriesTypeaheadSelected(row: CblIssueRow, event: SearchResult[]) {
    if (!event || event.length === 0) return;
    const selected = event[0];
    this.handleSeriesSelection(row, selected.seriesId, selected.name);
  }

  onChapterTypeaheadSelected(row: CblIssueRow, event: Chapter[]) {
    if (!event || event.length === 0) return;
    const chapter = event[0];
    this.handleChapterSelection(row, chapter);
  }

  toggleExpandRow(row: CblIssueRow) {
    this.table()?.rowDetail?.toggleExpandRow(row);
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
    if (!row.skipped) {
      // Collapse the detail row when skipping
      this.table()?.rowDetail?.collapseAllRows();
    }
    row.skipped = !row.skipped;
    // Force signal update
    this.allRows.set([...this.allRows()]);
  }

  openRemapRulesModal() {
    const ref = this.modalService.open(ManageRemapRulesModalComponent, {size: 'lg'});
    ref.closed.subscribe((hasModifications: boolean) => {
      if (hasModifications) {
        this.cblService.getRemapRules().subscribe(rules => {
          this.remapRules.set(rules);
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
      } : undefined;

      try {
        await this.cblService.finalizeImport(file.fileName, decisions, file.provider, repoMeta).toPromise();
      } catch {
        this.toastr.error(translate('toasts.failed-to-import', {name: file.name}));
      }
    }

    this.isProcessing.set(false);
    this.toastr.success(translate('toasts.import-complete'));
    this.modal.close(true);
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
      seriesTypeaheadSettings: this.createSeriesTypeahead(result),
      chapterTypeaheadSettings: result.seriesId > 0 ? this.createChapterTypeahead(result.seriesId) : null,
    }));

    this.allRows.set(rows);
  }

  private async handleSeriesSelection(row: CblIssueRow, seriesId: number, seriesName: string) {
    const confirmed = await this.confirmService.confirm(
      translate('toasts.save-remap-rule', {from: row.result.series, to: seriesName})
    );
    if (!confirmed) return;

    this.cblService.createRemapRule(row.result.series, seriesId).subscribe(rule => {
      row.remapRuleId = rule.id;
      this.remapRules.set([...this.remapRules(), rule]);
      this.validateCurrentFile();
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
      this.remapRules.set([...this.remapRules(), rule]);
      this.validateCurrentFile();
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
}
