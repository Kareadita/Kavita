import {DestroyRef, inject, Injectable, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {catchError, debounceTime, defer, EMPTY, filter, map, Observable, of, switchMap, tap} from 'rxjs';
import {ReadingProfile, ReadingProfileKind} from '../_models/preferences/reading-profiles';
import {ReadingProfileTarget} from '../_models/readers/reading-profile-target';
import {ReadingProfileService} from './reading-profile.service';
import {ReaderService} from './reader.service';

type SessionHooks = {
  apply: (profile: ReadingProfile, kind: 'open' | 'switch') => void;
  /**
   * Without it there is no snapshot, and autosave, updateParent and promote do nothing
   */
  pack?: () => ReadingProfile;
  loadParent?: boolean;
};

/**
 * Provide on each reader component, never in root: every reader needs its own profile, series and switch state
 */
@Injectable()
export class ReadingProfileSessionService {

  private readonly readingProfileService = inject(ReadingProfileService);
  private readonly readerService = inject(ReaderService);
  private readonly destroyRef = inject(DestroyRef);

  private _profile = signal<ReadingProfile | null>(null);
  private _parent = signal<ReadingProfile | null>(null);
  private _switching = signal(true);

  readonly profile = this._profile.asReadonly();
  readonly parent = this._parent.asReadonly();
  /**
   * True until open(), and while a switch is loading the new series' profile. The form still holds the old one
   */
  readonly switching = this._switching.asReadonly();

  private hooks: SessionHooks = {apply: () => {}};
  private target: ReadingProfileTarget | null = null;
  private savedSnapshot = '';
  private token = 0;

  attach(hooks: SessionHooks) {
    this.hooks = hooks;
  }

  open(target: ReadingProfileTarget, profile: ReadingProfile) {
    const token = ++this.token;
    this.target = {libraryId: target.libraryId, seriesId: target.seriesId};
    this.applyProfile(profile, 'open');
    this._switching.set(false);
    this.loadParent(token);
  }

  /**
   * Emits the applied profile, the old one if the GET fails, or completes empty when a newer switch started
   */
  switchTo$(target: ReadingProfileTarget & {seriesName: string}): Observable<ReadingProfile> {
    return defer(() => {
      const token = ++this.token;
      const previous = this._profile();
      this.target = {libraryId: target.libraryId, seriesId: target.seriesId};
      this._switching.set(true);

      return this.readingProfileService.getForSeries(target.libraryId, target.seriesId).pipe(
        filter(() => token === this.token),
        tap(profile => {
          this.applyProfile(profile, 'switch');
          this._switching.set(false);
          this.readerService.showReadingProfileSwitch(profile, target.seriesName);
          this.loadParent(token);
        }),
        catchError(err => {
          console.error('Failed to load the reading profile for series ' + target.seriesId, err);
          if (token !== this.token || previous === null) return EMPTY;

          this._switching.set(false);
          return of(previous);
        }),
        takeUntilDestroyed(this.destroyRef),
      );
    });
  }

  autosave(changes$: Observable<unknown>, debounceMs: number) {
    const pack = this.hooks.pack;
    if (!pack) return;

    changes$.pipe(
      debounceTime(debounceMs),
      filter(() => !this._switching() && this._profile() !== null),
      map(() => pack()),
      filter(profile => JSON.stringify(profile) !== this.savedSnapshot),
      tap(profile => this.savedSnapshot = JSON.stringify(profile)),
      // Before switchMap, so closing the reader lets a save in flight finish
      takeUntilDestroyed(this.destroyRef),
      switchMap(profile => {
        const target = this.target!;
        return this.readingProfileService.updateImplicit(target.libraryId, target.seriesId, profile).pipe(
          // A reading list can cross into another series before this returns
          filter(() => this.target === target),
          catchError(err => {
            console.error('Failed to update implicit profile:', err);
            return EMPTY;
          }),
        );
      }),
    ).subscribe(updated => this._profile.set(updated));
  }

  updateParent(): Observable<ReadingProfile> {
    const profile = this._profile();
    const target = this.target;
    const pack = this.hooks.pack;
    if (!profile || profile.kind !== ReadingProfileKind.Implicit || !target || !pack) return EMPTY;

    return this.readingProfileService.updateParentProfile(target.libraryId, target.seriesId, pack()).pipe(
      filter(() => this.target === target),
      tap(updated => this._profile.set(updated)),
    );
  }

  promote(): Observable<ReadingProfile> {
    const profile = this._profile();
    const target = this.target;
    if (!profile || profile.kind !== ReadingProfileKind.Implicit) return EMPTY;

    return this.readingProfileService.promoteProfile(profile.id).pipe(
      filter(() => this.target === target),
      tap(promoted => {
        this._profile.set(promoted);
        this._parent.set(promoted);
      }),
    );
  }

  private applyProfile(profile: ReadingProfile, kind: 'open' | 'switch') {
    this._profile.set(profile);
    this.hooks.apply(profile, kind);
    if (this.hooks.pack) {
      this.savedSnapshot = JSON.stringify(this.hooks.pack());
    }
  }

  private loadParent(token: number) {
    if (!this.hooks.loadParent) return;

    const profile = this._profile();
    const target = this.target;
    if (!profile || !target) return;

    if (profile.kind !== ReadingProfileKind.Implicit) {
      this._parent.set(profile);
      return;
    }

    this._parent.set(null);
    this.readingProfileService.getForSeries(target.libraryId, target.seriesId, true).pipe(
      filter(() => token === this.token),
      catchError(err => {
        console.error('Failed to load parent reading profile:', err);
        return EMPTY;
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(parent => this._parent.set(parent));
  }
}
