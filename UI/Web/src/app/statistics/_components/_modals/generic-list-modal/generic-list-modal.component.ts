import {Component, inject, Input} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { FilterPipe } from '../../../../_pipes/filter.pipe';
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
    selector: 'app-generic-list-modal',
    templateUrl: './generic-list-modal.component.html',
    styleUrls: ['./generic-list-modal.component.scss'],
    standalone: true,
    imports: [ReactiveFormsModule, FilterPipe, TranslocoDirective]
})
export class GenericListModalComponent {
  private readonly modal = inject(NgbActiveModal);

  @Input() items: Array<string> = [];
  @Input() title: string = '';
  @Input() clicked: ((item: string) => void) | undefined = undefined;

  listForm: FormGroup = new FormGroup({
    'filterQuery': new FormControl('', [])
  });

  filterList = (listItem: string) => {
    return listItem.toLowerCase().indexOf((this.listForm.value.filterQuery || '').toLowerCase()) >= 0;
  }

  close() {
    this.modal.close();
  }

  handleClick(item: string) {
    if (this.clicked) {
      this.clicked(item);
    }
  }
}
