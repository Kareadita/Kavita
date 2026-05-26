import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-confidence-chip',
  imports: [TranslocoDirective],
  templateUrl: './confidence-chip.component.html',
  styleUrl: './confidence-chip.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfidenceChipComponent {
  pct = input.required<number>();

  protected colorVar = computed(() => {
    const p = this.pct();
    if (p >= 90) return 'var(--match-confidence-chip-strong-color)';
    if (p >= 70) return 'var(--match-confidence-chip-likely-color)';
    if (p >= 55) return 'var(--match-confidence-chip-weak-color)';
    return 'var(--match-confidence-chip-doubt-color)';
  });

  protected labelKey = computed(() => {
    const p = this.pct();
    if (p >= 90) return 'strong';
    if (p >= 70) return 'likely';
    if (p >= 55) return 'weak';
    return 'doubt';
  });
}
