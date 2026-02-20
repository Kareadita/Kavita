import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {FilterPipe} from '../../../../_pipes/filter.pipe';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-generic-list-modal',
  templateUrl: './generic-list-modal.component.html',
  styleUrls: ['./generic-list-modal.component.scss'],
  imports: [ReactiveFormsModule, FilterPipe, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GenericListModalComponent {
  private readonly modal = inject(NgbActiveModal);

  items = input<string[]>([]);
  title = input<string>('');
  clicked = input<((item: string) => void) | undefined>(undefined);

  needsFilter = computed(() => this.items().length >= 5);

  listForm: FormGroup = new FormGroup({
    'filterQuery': new FormControl('', [])
  });

  filterList = (listItem: string) => {
    return listItem.toLowerCase().indexOf((this.listForm.value.filterQuery || '').toLowerCase()) >= 0;
  }

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
