import { Component, EventEmitter, Input } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { FilterPipe } from '../../../../pipe/filter.pipe';
import { NgIf, NgFor } from '@angular/common';

@Component({
    selector: 'app-generic-list-modal',
    templateUrl: './generic-list-modal.component.html',
    styleUrls: ['./generic-list-modal.component.scss'],
    standalone: true,
    imports: [ReactiveFormsModule, NgIf, NgFor, FilterPipe]
})
export class GenericListModalComponent {
  @Input() items: Array<string> = [];
  @Input() title: string = '';
  @Input() clicked: ((item: string) => void) | undefined = undefined;

  listForm: FormGroup = new FormGroup({
    'filterQuery': new FormControl('', [])
  });

  filterList = (listItem: string) => {
    return listItem.toLowerCase().indexOf((this.listForm.value.filterQuery || '').toLowerCase()) >= 0;
  }

  constructor(private modal: NgbActiveModal) {}

  close() {
    this.modal.close();
  }

  handleClick(item: string) {
    if (this.clicked) {
      this.clicked(item);
    }
  }
}
