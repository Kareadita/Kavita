import {ChangeDetectionStrategy, Component, computed, EventEmitter, input, Output, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-sort-button',
  imports: [
    TranslocoDirective
  ],
  templateUrl: './sort-button.component.html',
  styleUrl: './sort-button.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SortButtonComponent {

  // input is replacement for @Input
  disabled = input<boolean>(false);
  isAscending = input<boolean>(true);

  // signal is for internal state, whenever component needs to update the state internally. Not needed for disabled since component doesn't internally modify
  private isAscendingSignal = signal(this.isAscending());

  // Computed signals for template
  protected currentAscending = computed(() => this.isAscendingSignal());
  protected isDisabled = computed(() => this.disabled());

  @Output() update = new EventEmitter<boolean>();

  updateSortOrder() {
    this.isAscendingSignal.set(!this.isAscendingSignal());
    this.update.emit(this.isAscendingSignal());
  }
}
