import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-match-status-dot',
  imports: [TranslocoDirective],
  templateUrl: './match-status-dot.component.html',
  styleUrl: './match-status-dot.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MatchStatusDotComponent {
  endDate = input<string | null | undefined>(undefined);

  protected isOngoing = computed(() => !this.endDate());
}
