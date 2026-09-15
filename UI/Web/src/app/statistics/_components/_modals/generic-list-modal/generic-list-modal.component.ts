import {ChangeDetectionStrategy, Component, computed, inject, input, signal} from '@angular/core';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {TranslocoDirective} from "@jsverse/transloco";
import {FilterFieldComponent} from "../../../../shared/_components/filter-field/filter-field.component";
import {filteredBy} from "../../../../_helpers/filtered";

@Component({
  selector: 'app-generic-list-modal',
  templateUrl: './generic-list-modal.component.html',
  styleUrls: ['./generic-list-modal.component.scss'],
  imports: [TranslocoDirective, FilterFieldComponent],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GenericListModalComponent {
  private readonly modal = inject(NgbActiveModal);

  items = input<string[]>([]);
  title = input<string>('');
  clicked = input<((item: string) => void) | undefined>(undefined);

  needsFilter = computed(() => this.items().length >= 5);

  filterQuery = signal('');
  protected readonly filteredItems = filteredBy(this.items, this.filterQuery);

  close() {
    this.modal.dismiss();
  }

  handleClick(item: string) {
    const clickFn = this.clicked();
    if (clickFn) {
      clickFn(item);
    }
  }
}
