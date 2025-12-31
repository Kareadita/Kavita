import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ContentChild,
  inject, input,
  model,
  OnInit,
  signal,
  TemplateRef, viewChild
} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {toSignal} from "@angular/core/rxjs-interop";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {NgTemplateOutlet} from "@angular/common";

export type ListSelectionItem<T> = {
  label: string,
  value: T,
}


@Component({
  selector: 'app-list-select-modal',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    SentenceCasePipe,
    NgTemplateOutlet
  ],
  templateUrl: './list-select-modal.component.html',
  styleUrl: './list-select-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ListSelectModalComponent<T> implements OnInit {

  private readonly modal = inject(NgbActiveModal);

  defaultTemplate = viewChild.required<TemplateRef<any>>('defaultTemplate');

  title = model.required<string>();
  description = model<string | null>(null);
  items = model.required<ListSelectionItem<T>[]>();
  preSelectedItems = model<T[]>([]);
  itemsBeforeFilter = model(8);
  requireConfirmation = model(false);
  showFooter = model(true);
  multiSelect = model(false);
  itemTemplate = input<TemplateRef<any> | null>(null);

  protected finalItemTemplate = computed(() => {
    const defaultTemplate = this.defaultTemplate();
    const itemTemplate = this.itemTemplate();

    if (itemTemplate) {
      return itemTemplate;
    }

    return defaultTemplate;
  })

  protected selectedItems = signal<ListSelectionItem<T>[]>([]);

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

  ngOnInit() {
    const items = this.items().filter(item => this.preSelectedItems().includes(item.value));
    this.selectedItems.set(items);
  }

  select(item: ListSelectionItem<T>) {
    if (this.multiSelect()) {
      const currentlySelected = this.selectedItems().includes(item);
      if (currentlySelected) {
        this.selectedItems.update(x => [...x.filter(i => i !== item)])
      } else {
        this.selectedItems.update(x => [...x, item])
      }


    } else {
      this.selectedItems.set([item]);
    }

    if (!this.requireConfirmation() && !this.multiSelect()) {
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
    if (this.multiSelect()) {
      this.modal.close(this.selectedItems().map(i => i.value))
    } else {
      this.modal.close(this.selectedItems()[0].value);
    }
  }

}
