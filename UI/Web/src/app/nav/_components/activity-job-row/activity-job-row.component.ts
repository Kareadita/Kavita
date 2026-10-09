import {ChangeDetectionStrategy, Component, computed, inject, input, output} from '@angular/core';
import {PercentPipe} from '@angular/common';
import {Router} from '@angular/router';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {ToastrService} from '@openng/ngx-toastr';
import {Observable} from 'rxjs';
import {ActivityJob} from '../../../_models/activity/activity-job';
import {ActivityStep} from '../../../_models/activity/activity-step';
import {currentStep, isFinishingStep, isMultiLibraryJob, isProcessingDone, isScanJob, jobProgress, titleStep} from '../../../_helpers/activity-job-progress';
import {EventTitlePipe} from '../../../_pipes/event-title.pipe';
import {EventActionPipe} from '../../../_pipes/event-action.pipe';
import {ActivityAgePipe} from '../../../_pipes/activity-age.pipe';
import {ActivityDurationPipe} from '../../../_pipes/activity-duration.pipe';
import {EventAction} from '../../../_models/events/event-action';
import {ActivityEndReason} from '../../../_models/activity/activity-end-reason';
import {LibraryService} from '../../../_services/library.service';
import {LibraryScanSummary} from '../../../_models/activity/library-scan-summary';
import {SeriesService} from '../../../_services/series.service';
import {EVENTS} from '../../../_services/message-hub.service';
import {SeriesScanTarget} from '../../../_models/events/bodies/series-scan-target';

interface StepCounter {
  current: number;
  total: number;
}

interface SummaryPart {
  key: string;
  params: Record<string, number>;
}

@Component({
  selector: 'app-activity-job-row',
  templateUrl: './activity-job-row.component.html',
  styleUrl: './activity-job-row.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, PercentPipe, EventTitlePipe, EventActionPipe, ActivityAgePipe, ActivityDurationPipe]
})
export class ActivityJobRowComponent {
  private readonly router = inject(Router);
  private readonly libraryService = inject(LibraryService);
  private readonly seriesService = inject(SeriesService);
  private readonly toastr = inject(ToastrService);

  readonly job = input.required<ActivityJob>();
  readonly now = input.required<number>();
  readonly libraryNames = input<Record<number, string>>();
  readonly pinned = input(false);
  readonly navigated = output<void>();
  readonly dismissed = output<string>();

  protected readonly ended = computed(() => this.job().endedUtc !== null);
  protected readonly endedAway = computed(() => this.job().endReason === ActivityEndReason.Away);
  protected readonly failed = computed(() => this.job().endReason === ActivityEndReason.Failed);
  protected readonly isScan = computed(() => isScanJob(this.job()));
  protected readonly isMultiLibrary = computed(() => isMultiLibraryJob(this.job()));
  protected readonly libraryCount = computed(() => Math.max(Object.keys(this.libraryNames() ?? {}).length, this.job().libraryIds.length));
  protected readonly progress = computed(() => jobProgress(this.job(), this.libraryCount()));
  protected readonly titleStep = computed(() => titleStep(this.job()));
  protected readonly step = computed(() => currentStep(this.job()));
  protected readonly counter = computed(() => counterOf(this.step()));
  protected readonly seriesScan = computed(() => {
    if (this.job().scanSummaries.some(s => s.seriesRemoved > 0)) return null;
    return seriesScanOf(this.titleStep());
  });

  protected readonly finishingStep = computed(() => {
    const step = this.step();
    return this.isScan() && step && isFinishingStep(step) ? step : null;
  });
  protected readonly finishingUp = computed(() => isProcessingDone(this.step()));
  protected readonly seriesFinishingLabel = computed(() => {
    const step = this.finishingStep();
    if (!step || !this.seriesScan()) return null;

    switch (step.name) {
      case EVENTS.CoverUpdateProgress:
        return translate('events-widget.refreshing-covers-label');
      case EVENTS.WordCountAnalyzerProgress:
        return translate('events-widget.counting-words-label');
      default:
        return null;
    }
  });

  protected readonly currentLibraryName = computed(() => {
    const libraryId = this.job().libraryIds.at(-1);
    return libraryId === undefined ? '' : (this.libraryNames()?.[libraryId] ?? '');
  });

  protected readonly summaryParts = computed(() => summaryPartsOf(this.job().scanSummaries));
  protected readonly showSummary = computed(() => this.ended() && this.isScan() && this.job().scanSummaries.length > 0);
  protected readonly canOpen = computed(() => this.ended() && this.isScan() && !this.isMultiLibrary() && this.job().libraryId !== null);
  protected readonly canRescan = computed(() => this.isScan() && (this.isMultiLibrary() || this.job().libraryId !== null));

  protected open() {
    const seriesScan = this.seriesScan();
    if (seriesScan) {
      this.router.navigate(['library', this.job().libraryId, 'series', seriesScan.seriesId]);
    } else {
      this.router.navigate(['library', this.job().libraryId]);
    }
    this.navigated.emit();
  }

  protected rescan() {
    const job = this.job();
    const libraryId = job.libraryId!;
    const seriesScan = this.seriesScan();

    let request: Observable<unknown>;
    let toast: string;
    if (seriesScan) {
      request = this.seriesService.scan(libraryId, seriesScan.seriesId);
      toast = translate('toasts.scan-queued', {name: seriesScan.seriesName});
    } else if (this.isMultiLibrary()) {
      request = this.libraryService.scanAll();
      toast = translate('toasts.scan-all-queued');
    } else {
      request = this.libraryService.scan(libraryId);
      toast = translate('toasts.scan-queued', {name: this.libraryNames()?.[libraryId] ?? ''});
    }

    request.subscribe(() => {
      this.toastr.info(toast);
      this.dismissed.emit(job.id);
    });
  }

  protected readonly EventAction = EventAction;
}

function summaryPartsOf(summaries: LibraryScanSummary[]): SummaryPart[] {
  const sum = (field: keyof Omit<LibraryScanSummary, 'libraryId' | 'libraryName'>) => summaries.reduce((total, s) => total + s[field], 0);

  const changes: SummaryPart[] = [
    {key: 'scan-series-added-label', params: {count: sum('seriesAdded')}},
    {key: 'scan-series-removed-label', params: {count: sum('seriesRemoved')}},
    {key: 'scan-chapters-added-label', params: {count: sum('chaptersAdded')}},
    {key: 'scan-chapters-updated-label', params: {count: sum('chaptersUpdated')}},
    {key: 'scan-chapters-removed-label', params: {count: sum('chaptersRemoved')}},
  ].filter(p => p.params.count > 0);

  const parts = changes.length > 0 ? changes : [{key: 'scan-no-changes-label', params: {}}];

  const problemFiles = sum('problemFiles');
  const newProblemFiles = sum('newProblemFiles');
  if (newProblemFiles > 0) {
    parts.push({key: 'scan-problem-files-new-label', params: {count: problemFiles, new: newProblemFiles}});
  } else if (problemFiles > 0) {
    parts.push({key: 'scan-problem-files-label', params: {count: problemFiles}});
  }

  return parts;
}

function seriesScanOf(step: ActivityStep | undefined): SeriesScanTarget | null {
  return (step?.body as {seriesScan?: SeriesScanTarget | null} | null)?.seriesScan ?? null;
}

function counterOf(step: ActivityStep | undefined): StepCounter | null {
  const body = (step?.body ?? {}) as Partial<{current: number; total: number; leftToProcess: number; totalToProcess: number}>;

  if (typeof body.current === 'number' && typeof body.total === 'number' && body.total > 0) {
    return {current: body.current, total: body.total};
  }
  if (typeof body.leftToProcess === 'number' && typeof body.totalToProcess === 'number' && body.totalToProcess > 0) {
    return {current: body.totalToProcess - body.leftToProcess, total: body.totalToProcess};
  }
  return null;
}
