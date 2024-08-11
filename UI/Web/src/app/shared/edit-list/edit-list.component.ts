import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component, DestroyRef,
  EventEmitter,
  inject,
  Input,
  OnInit,
  Output
} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {Select2Module} from "ng-select2-component";
import {TranslocoDirective} from "@jsverse/transloco";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {debounceTime, distinctUntilChanged, tap} from "rxjs/operators";

@Component({
  selector: 'app-edit-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, Select2Module, TranslocoDirective],
  templateUrl: './edit-list.component.html',
  styleUrl: './edit-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditListComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  @Input({required: true}) items: Array<string> = [];
  @Input({required: true}) label = '';
  @Output() updateItems = new EventEmitter<Array<string>>();

  form: FormGroup = new FormGroup({});
  private combinedItems: string = '';

  get Items() {
    return this.combinedItems.split(',') || [''];
  }


  ngOnInit() {
    this.items.forEach((link, index) => {
      this.form.addControl('link' + index, new FormControl(link, []));
    });

    this.combinedItems = this.items.join(',');

    this.form.valueChanges.pipe(
      debounceTime(100),
      distinctUntilChanged(),
      tap(data => this.emit()),
      takeUntilDestroyed(this.destroyRef))
    .subscribe();
    this.cdRef.markForCheck();
  }

  add() {
    this.combinedItems += ',';
    this.form.addControl('link' + (this.Items.length - 1), new FormControl('', []));
    this.emit();
    this.cdRef.markForCheck();
  }

  remove(index: number) {

    const initialControls = Object.keys(this.form.controls)
      .filter(key => key.startsWith('link'));

    if (index == 0 && initialControls.length === 1) {
      this.form.get(initialControls[0])?.setValue('', {emitEvent: true});
      this.emit();
      this.cdRef.markForCheck();
      return;
    }

    // Remove the form control explicitly then rebuild the combinedItems
    this.form.removeControl('link' + index, {emitEvent: true});

    this.combinedItems = Object.keys(this.form.controls)
      .filter(key => key.startsWith('link'))
      .map(key => this.form.get(key)?.value)
      .join(',');


    this.emit();
    this.cdRef.markForCheck();
  }

  emit() {
    this.updateItems.emit(Object.keys(this.form.controls)
    .filter(key => key.startsWith('link'))
    .map(key => this.form.get(key)?.value)
    .filter(v => v !== null && v !== ''));
  }
}
