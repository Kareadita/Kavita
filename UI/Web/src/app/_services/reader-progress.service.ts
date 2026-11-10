import {inject, Injectable, OnDestroy} from '@angular/core';
import {catchError, concatMap, EMPTY, Subject, switchMap} from 'rxjs';
import {ReaderService} from './reader.service';
import {ProgressSave} from '../_models/readers/progress-save';

/**
 * Provide on each reader component, never in root: every reader needs its own queue and last save
 */
@Injectable()
export class ReaderProgressService implements OnDestroy {

  private readonly readerService = inject(ReaderService);

  private readonly saves$ = new Subject<ProgressSave>();
  private lastSaved: ProgressSave | null = null;

  constructor() {
    // Not tied to a DestroyRef: readers save right before closing, ngOnDestroy complete() the queue so it drains
    this.saves$.pipe(
      concatMap(save => this.send(save)),
    ).subscribe();
  }

  save(save: ProgressSave) {
    if (!save.resetFirst && this.isSameAsLastSaved(save)) return;

    this.lastSaved = save;
    this.saves$.next(save);
  }

  ngOnDestroy() {
    this.saves$.complete();
  }

  private isSameAsLastSaved(save: ProgressSave) {
    const last = this.lastSaved;
    return last !== null
      && last.chapterId === save.chapterId
      && last.page === save.page
      && (last.bookScrollId ?? null) === (save.bookScrollId ?? null);
  }

  private send(save: ProgressSave) {
    const {libraryId, seriesId, volumeId, chapterId, page} = save;
    const bookScrollId = save.bookScrollId ?? null;
    const save$ = this.readerService.saveProgress(libraryId, seriesId, volumeId, chapterId, page, bookScrollId);
    const send$ = save.resetFirst
      ? this.readerService.saveProgress(libraryId, seriesId, volumeId, chapterId, 0).pipe(switchMap(() => save$))
      : save$;

    return send$.pipe(
      catchError(() => {
        if (this.lastSaved === save) {
          this.lastSaved = null;
        }
        return EMPTY;
      }),
    );
  }
}
