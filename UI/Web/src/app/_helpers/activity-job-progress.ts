import {EVENTS} from '../_services/message-hub.service';
import {ActivityJob} from '../_models/activity/activity-job';
import {ActivityStep} from '../_models/activity/activity-step';
import {MessageEventCode} from '../_models/events/core/message-event-code';

const ScanStepNames: string[] = [EVENTS.FileScanProgress, EVENTS.ScanProgress];
const FinishStepNames: string[] = [EVENTS.CoverUpdateProgress, EVENTS.WordCountAnalyzerProgress];

export interface JobSegment {
  /**
   * Finding files 1, processing series 3, finishing up 1
   */
  weight: number;
  /**
   * 0 to 1
   */
  fill: number;
  indeterminate: boolean;
}

export interface JobProgress {
  /**
   * 0 to 1 over the whole job. Null when the job only reports indeterminate work
   */
  value: number | null;
  indeterminate: boolean;
  /**
   * Scan jobs only. Each step code fills its own segment, so a second folder pass never moves the bar backward
   */
  segments: JobSegment[] | null;
}

export function isScanJob(job: ActivityJob) {
  return Object.keys(job.steps).some(name => ScanStepNames.includes(name));
}

export function isFinishingStep(step: ActivityStep) {
  return FinishStepNames.includes(step.name);
}

/**
 * ScanProgress only ends after the commit. The update sent after the last series carries 1, per-series updates stop at (N-1)/N
 */
export function isProcessingDone(step: ActivityStep | undefined) {
  return step?.name === EVENTS.ScanProgress && (step.eventType === 'ended' || step.progress === 1);
}

export function isMultiLibraryJob(job: ActivityJob) {
  return job.libraryIds.length > 1;
}

/**
 * Latest step that has not ended, else the latest step. During the finish grace every step has ended but the job still runs
 */
export function currentStep(job: ActivityJob): ActivityStep | undefined {
  const steps = Object.values(job.steps).sort((a, b) => Date.parse(b.updatedUtc) - Date.parse(a.updatedUtc));
  return steps.find(s => s.eventType !== 'ended') ?? steps[0];
}

/**
 * The step a row is titled by, so a scan still reads "Scanning Manga" while covers refresh
 */
export function titleStep(job: ActivityJob): ActivityStep | undefined {
  if (!isScanJob(job)) return currentStep(job);
  return job.steps[EVENTS.ScanProgress] ?? job.steps[EVENTS.FileScanProgress];
}

/**
 * @param libraryCount Total libraries on the server, for "x of y" when one job scans every library
 */
export function jobProgress(job: ActivityJob, libraryCount: number = 0): JobProgress {
  if (!isScanJob(job)) {
    if (job.endedUtc !== null) return {value: 1, indeterminate: false, segments: null};

    const value = currentStep(job)?.progress ?? null;
    return {value, indeterminate: value === null, segments: null};
  }

  const segments = scanSegments(job);
  const totalWeight = segments.reduce((sum, s) => sum + s.weight, 0);
  const libraryValue = segments.reduce((sum, s) => sum + s.weight * s.fill, 0) / totalWeight;
  const indeterminate = segments.some(s => s.indeterminate);

  if (!isMultiLibraryJob(job)) return {value: libraryValue, indeterminate, segments};

  const total = Math.max(libraryCount, job.libraryIds.length);
  const value = job.endedUtc !== null ? 1 : (job.libraryIds.length - 1 + libraryValue) / total;
  return {value, indeterminate, segments: null};
}

function scanSegments(job: ActivityJob): JobSegment[] {
  if (job.endedUtc !== null) {
    return [{weight: 1, fill: 1, indeterminate: false}, {weight: 3, fill: 1, indeterminate: false}, {weight: 1, fill: 1, indeterminate: false}];
  }

  const libraryId = job.libraryIds.at(-1);
  const steps = Object.values(job.steps).filter(s => libraryId === undefined || libraryIdOf(s) === libraryId);
  const fileScan = steps.find(s => s.name === EVENTS.FileScanProgress);
  const scan = steps.find(s => s.name === EVENTS.ScanProgress);
  const processingDone = isProcessingDone(scan);

  const findDone = !!scan || fileScan?.eventType === 'ended';
  const find: JobSegment = {
    weight: 1,
    fill: findDone ? 1 : findingFill(fileScan),
    indeterminate: !findDone && fileScan !== undefined && fileScan.progress === null,
  };

  const process: JobSegment = {
    weight: 3,
    fill: processingDone ? 1 : (scan?.progress ?? 0),
    indeterminate: !processingDone && scan !== undefined && scan.progress === null,
  };

  // CoverUpdateProgress reports 0 for every series, so finishing usually has no fill until the job ends
  const finishers = processingDone ? steps.filter(s => isFinishingStep(s) && s.eventType !== 'ended') : [];
  const finishFill = Math.max(0, ...finishers.map(s => s.progress ?? 0));
  const finish: JobSegment = {weight: 1, fill: finishFill, indeterminate: finishers.length > 0 && finishFill === 0};

  return [find, process, finish];
}

function findingFill(step: ActivityStep | undefined) {
  if (!step) return 0;

  const progress = step.progress ?? 0;
  switch (step.code) {
    case MessageEventCode.ScanListingFolders:
      return progress / 2;
    case MessageEventCode.ScanReadingFiles:
      return 0.5 + progress / 2;
    case MessageEventCode.ScanGroupingSeries:
      return 1;
    default:
      return progress;
  }
}

function libraryIdOf(step: ActivityStep) {
  const body = step.body as {libraryId?: unknown} | null;
  return typeof body?.libraryId === 'number' ? body.libraryId : undefined;
}
