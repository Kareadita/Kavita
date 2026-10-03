import {inject, Injectable, signal} from '@angular/core';
import {ServerService} from './server.service';
import {ActivitySnapshot} from '../_models/activity/activity-snapshot';

@Injectable({
  providedIn: 'root'
})
export class ActivitySnapshotService {
  private readonly serverService = inject(ServerService);

  private _snapshot = signal<ActivitySnapshot | null>(null);
  readonly snapshot = this._snapshot.asReadonly();

  refresh() {
    this.serverService.getActivity().subscribe({
      next: snapshot => this._snapshot.set(snapshot),
      error: () => {},
    });
  }
}
