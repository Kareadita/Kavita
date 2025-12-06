import {ChangeDetectionStrategy, Component, computed, inject, model, signal} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {toSignal} from "@angular/core/rxjs-interop";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";

export type ListSelectionItem<T> = {
  label: string,
  value: T,
}


@Component({
  selector: 'app-list-select-modal',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SentenceCasePipe
  ],
  templateUrl: './list-select-modal.component.html',
  styleUrl: './list-select-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ListSelectModalComponent<T> {

  private readonly modal = inject(NgbActiveModal);

  title = model.required<string>();
  description = model<string | null>(null);
  items = model.required<ListSelectionItem<T>[]>();
  itemsBeforeFilter = model(8);
  requireConfirmation = model(false);
  showFooter = model(true);

  protected selectedItem = signal<ListSelectionItem<T> | null>(null);

  protected filteredItems = computed(() => {
    const items = this.items();
    const filter = this.filterQuery().toLowerCase();

    if (!filter) return items;

    return items.filter(item => item.label.toLowerCase().includes(filter));
  });

  protected filterForm = new FormGroup({
    query: new FormControl('', {nonNullable: true}),
  });
  protected filterQuery = toSignal(this.filterForm.get('query')!.valueChanges, {initialValue: ''})

  select(item: ListSelectionItem<T>) {
    this.selectedItem.set(item);

    if (!this.requireConfirmation()) {
      this.confirm();
      return;
    }
  }

  clear() {
    this.filterForm.get('query')?.setValue('');
  }

  close() {
    this.modal.dismiss();
  }

  confirm() {
    this.modal.close(this.selectedItem()?.value);
  }

}
