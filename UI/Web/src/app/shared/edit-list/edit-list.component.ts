import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {AsyncValidatorFn, FormArray, FormControl, FormGroup, ReactiveFormsModule, ValidatorFn} from "@angular/forms";
import {TranslocoDirective} from "@jsverse/transloco";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged, tap} from "rxjs/operators";

@Component({
    selector: 'app-edit-list',
    imports: [ReactiveFormsModule, TranslocoDirective],
    templateUrl: './edit-list.component.html',
    styleUrl: './edit-list.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditListComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  @Input({required: true}) items: Array<string> = [];
  @Input({required: true}) label = '';
  @Input() validators: ValidatorFn[] = []
  @Input() asyncValidators: AsyncValidatorFn[] = [];
  // TODO: Make this more dynamic based on which validator failed
  @Input() errorMessage: string | null = null;
  @Output() updateItems = new EventEmitter<Array<string>>();

  form: FormGroup = new FormGroup({items: new FormArray([])});

  get ItemsArray(): FormArray {
    return this.form.get('items') as FormArray;
  }


  ngOnInit() {
    this.items.forEach(item => this.addItem(item));
    if (this.items.length === 0) {
      this.addItem("");
    }


    this.form.valueChanges.pipe(
      debounceTime(100),
      distinctUntilChanged(),
      tap(data => this.emit()),
      takeUntilDestroyed(this.destroyRef))
    .subscribe();
    this.cdRef.markForCheck();
  }

  createItemControl(value: string = ''): FormControl {
    return new FormControl(value, this.validators, this.asyncValidators);
  }

  add() {
    this.ItemsArray.push(this.createItemControl());
    this.emit();
    this.cdRef.markForCheck();
  }

  addItem(value: string) {
    this.ItemsArray.push(this.createItemControl(value));
  }

  remove(index: number) {
    // If it's the last item, just clear its value
    if (this.ItemsArray.length === 1) {
      this.ItemsArray.at(0).setValue('');
      this.emit();
      this.cdRef.markForCheck();
      return;
    }

    this.ItemsArray.removeAt(index);
    this.emit();
    this.cdRef.markForCheck();
  }

  // Emit non-empty item values
  emit() {
    const nonEmptyItems = this.ItemsArray.controls
      .map(control => control.value)
      .filter(value => value !== null && value.trim() !== '');

    this.updateItems.emit(nonEmptyItems);
  }
}
