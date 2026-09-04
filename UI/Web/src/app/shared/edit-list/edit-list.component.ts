import {ChangeDetectionStrategy, Component, computed, input, OnInit, output, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {applyEach, form, FormField, validate, validateAsync, ValidationResult} from "@angular/forms/signals";
import {ValidationErrorsComponent} from "../_components/validation-errors/validation-errors.component";
import {Observable, of} from "rxjs";
import {rxResource} from "@angular/core/rxjs-interop";

interface EditListFormModel {
  items: string[];
}

@Component({
  selector: 'app-edit-list',
  imports: [TranslocoDirective, FormField, ValidationErrorsComponent],
  templateUrl: './edit-list.component.html',
  styleUrl: './edit-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditListComponent implements OnInit {

  items = input.required<string[]>();
  label = input<string>('');
  itemValidator = input<((value: string) => ValidationResult | null) | null>(null);
  asyncItemValidator = input<((value: string) => Observable<ValidationResult> | null) | null>(null);
  messages = input<Record<string, string>>({});
  /** Only emits non-empty items */
  readonly updateItems = output<Array<string>>();

  private readonly formModel = signal<EditListFormModel>({
    items: [],
  });
  formGroup = form(this.formModel, (path) => {
    applyEach(path.items, (item) => {
      validate(item, (ctx) => this.itemValidator()?.(ctx.value()) ?? []);

      validateAsync(item, {
        params: (ctx) => {
          const value = ctx.value();
          if (!this.asyncItemValidator() || value.trim().length === 0) { return undefined; }
          return value;
        },
        debounce: 300,
        factory: (params) => rxResource({
          params,
          stream: ({params: value}) =>  this.asyncItemValidator()?.(value) ?? of(null)
        }),
        onSuccess: (result) => result,
        onError: () => []
      });
    });
  });

  hasOneItem = computed(() => {
    return this.formGroup.items().value().length === 1;
  });

  disableRemove = computed(() => {
    const items = this.formGroup.items().value();
    return (items.length === 1 && items[0] === '');
  });


  ngOnInit() {
    const items = this.items();
    if (items.length === 0) {
      items.push('');
    }
    this.formGroup.items().value.set(items);
  }


  add() {
    this.formGroup.items().value.update(x => [...x, '']);
    this.emit();
  }

  remove(index: number) {
    // If it's the last item, just clear its value
    if (this.hasOneItem()) {
      this.formGroup.items().value.set(['']);
      this.emit();
      return;
    }

    this.formGroup.items().value.update(x => {
      x.splice(index, 1);
      return [...x];
    });
    this.emit();
  }

  emit() {
    this.updateItems.emit(this.formModel().items.filter(value => value !== null && value.trim() !== ''));
  }
}
