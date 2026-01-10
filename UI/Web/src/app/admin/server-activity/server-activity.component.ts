import {ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {ActivityCardComponent} from "../../_single-module/activity-card/activity-card.component";
import {ActivityService} from "../../_services/activity.service";
import {ReadingSession} from "../../_models/progress/reading-session";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {debounceTime, filter} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-server-activity',
  imports: [
    TranslocoDirective,
    ActivityCardComponent
  ],
  templateUrl: './server-activity.component.html',
  styleUrl: './server-activity.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ServerActivityComponent implements OnInit {

  private readonly activityService = inject(ActivityService);
  protected readonly messageHub = inject(MessageHubService);
  protected readonly destroyRef = inject(DestroyRef);

  activeSessions = signal<ReadingSession[]>([]);

  constructor() {
    this.messageHub.messages$.pipe(
      filter(event => event.event === EVENTS.UserProgressUpdate || event.event === EVENTS.SessionClose),
      debounceTime(100),
      takeUntilDestroyed(this.destroyRef)).subscribe(_ => {
        this.loadData();
    });
  }

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.activityService.getActiveSessions().subscribe(sessions => {
      this.activeSessions.set([...sessions]);
    });
  }

}
