import {ChangeDetectionStrategy, Component, computed, inject, input, output} from '@angular/core';
import {PercentPipe} from '@angular/common';
import {Router} from '@angular/router';
import {TranslocoDirective} from '@jsverse/transloco';
import {ActivityJob} from '../../../_models/activity/activity-job';
import {ActivityStep} from '../../../_models/activity/activity-step';
import {currentStep, isFinishingStep, isMultiLibraryJob, isScanJob, jobProgress, titleStep} from '../../../_helpers/activity-job-progress';
import {EventTitlePipe} from '../../../_pipes/event-title.pipe';
import {EventActionPipe} from '../../../_pipes/event-action.pipe';
import {ActivityAgePipe} from '../../../_pipes/activity-age.pipe';
import {ActivityDurationPipe} from '../../../_pipes/activity-duration.pipe';
import {EventAction} from '../../../_models/events/event-action';

interface StepCounter {
  current: number;
  total: number;
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

  readonly job = input.required<ActivityJob>();
  readonly now = input.required<number>();
  readonly libraryNames = input<Record<number, string>>();
  readonly navigated = output<void>();

  protected readonly ended = computed(() => this.job().endedUtc !== null);
  protected readonly isScan = computed(() => isScanJob(this.job()));
  protected readonly isMultiLibrary = computed(() => isMultiLibraryJob(this.job()));
  protected readonly libraryCount = computed(() => Math.max(Object.keys(this.libraryNames() ?? {}).length, this.job().libraryIds.length));
  protected readonly progress = computed(() => jobProgress(this.job(), this.libraryCount()));
  protected readonly titleStep = computed(() => titleStep(this.job()));
  protected readonly step = computed(() => currentStep(this.job()));
  protected readonly counter = computed(() => counterOf(this.step()));

  protected readonly finishingStep = computed(() => {
    const step = this.step();
    return this.isScan() && step && isFinishingStep(step) ? step : null;
  });

  protected readonly currentLibraryName = computed(() => {
    const libraryId = this.job().libraryIds.at(-1);
    return libraryId === undefined ? '' : (this.libraryNames()?.[libraryId] ?? '');
  });

  protected readonly showSummary = computed(() => this.ended() && this.isScan() && this.job().seenFromStart);
  protected readonly canOpenLibrary = computed(() => this.ended() && this.isScan() && !this.isMultiLibrary() && this.job().libraryId !== null);

  protected openLibrary() {
    this.router.navigate(['library', this.job().libraryId]);
    this.navigated.emit();
  }

  protected readonly EventAction = EventAction;
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
