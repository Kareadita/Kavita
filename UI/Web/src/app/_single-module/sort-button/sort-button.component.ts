import {ChangeDetectionStrategy, Component, input, model} from '@angular/core';
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

  disabled = input<boolean>(false);
  isAscending = model<boolean>(true);

  updateSortOrder() {
    this.isAscending.set(!this.isAscending());
  }
}
