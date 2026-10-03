import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';

@Component({
  selector: 'app-events-widget-icon',
  templateUrl: './events-widget-icon.component.html',
  styleUrl: './events-widget-icon.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EventsWidgetIconComponent {
  /**
   * 0 to 1 for the slowest running job, null when nothing determinate is running
   */
  readonly runningProgress = input<number | null>(null);
  /**
   * A job is running with no known length. Ignored when runningProgress is set
   */
  readonly indeterminate = input<boolean>(false);
  readonly attentionCount = input<number>(0);
  readonly hasError = input<boolean>(false);
  readonly offline = input<boolean>(false);
  readonly readingCount = input<number>(0);

  protected readonly showRing = computed(() => !this.offline() && this.runningProgress() !== null);
  protected readonly showSpinner = computed(() => !this.offline() && this.runningProgress() === null && this.indeterminate());
  protected readonly ringDash = computed(() => `${Math.round(Math.min(Math.max(this.runningProgress() ?? 0, 0), 1) * 100)} 100`);
  protected readonly badgeText = computed(() => this.attentionCount() > 99 ? '99+' : `${this.attentionCount()}`);
}
