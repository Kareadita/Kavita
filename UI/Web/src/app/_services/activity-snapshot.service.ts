import {computed, DestroyRef, inject, Injectable, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {catchError, EMPTY, map, Subject, switchMap} from 'rxjs';
import {ServerService} from './server.service';
import {ActivitySnapshotResult} from '../_models/activity/activity-snapshot-result';

@Injectable({
  providedIn: 'root'
})
export class ActivitySnapshotService {
  private readonly serverService = inject(ServerService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly requests = new Subject<void>();
  private _result = signal<ActivitySnapshotResult | null>(null);
  readonly result = this._result.asReadonly();
  readonly snapshot = computed(() => this._result()?.snapshot ?? null);

  constructor() {
    this.requests.pipe(
      switchMap(() => {
        const requestedAtMs = Date.now();
        return this.serverService.getActivity().pipe(
          map(snapshot => ({snapshot, requestedAtMs})),
          catchError(() => EMPTY),
        );
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(result => this._result.set(result));
  }

  refresh() {
    this.requests.next();
  }
}
