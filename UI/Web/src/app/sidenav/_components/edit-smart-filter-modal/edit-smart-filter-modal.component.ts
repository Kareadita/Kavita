import {ChangeDetectionStrategy, Component, DestroyRef, inject, model, OnInit, signal} from '@angular/core';
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {TranslocoDirective} from "@jsverse/transloco";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {FilterService} from "../../../_services/filter.service";
import {debounceTime, distinctUntilChanged, switchMap} from "rxjs/operators";
import {of, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {modalSaved} from "../../../_models/modal/modal-result";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";

@Component({
  selector: 'app-edit-smart-filter-modal',
  imports: [
    TranslocoDirective,
    SentenceCasePipe,
    ReactiveFormsModule, FormFieldDirective, ValidationErrorsComponent],
  templateUrl: './edit-smart-filter-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './edit-smart-filter-modal.component.scss'
})
export class EditSmartFilterModalComponent implements OnInit {

  private readonly modal = inject(NgbActiveModal);
  private readonly filterService = inject(FilterService);
  private readonly destroyRef = inject(DestroyRef);

  smartFilterForm: FormGroup = new FormGroup({
    'name': new FormControl('', [Validators.required]),
  });

  smartFilter = model.required<SmartFilter>();
  allFilters = signal<SmartFilter[]>([]);

  ngOnInit(): void {

    this.filterService.getAllFilters().subscribe(data => {
      this.allFilters.set(data);
    });

    this.smartFilterForm.get('name')!.setValue(this.smartFilter().name);

    this.smartFilterForm.get('name')!.valueChanges.pipe(
      debounceTime(100),
      distinctUntilChanged(),
      switchMap(name => {
        const other = this.allFilters().find(f => {
          return f.id !== this.smartFilter().id && f.name === name;
        })
        return of(other !== undefined)
      }),
      tap((exists) => {
        const isThisSmartFilter = this.smartFilter().name === this.smartFilterForm.get('name')!.value;
        const empty = (this.smartFilterForm.get('name')!.value as string).trim().length === 0;

        if (!exists || isThisSmartFilter) {
          if (!empty) {
            this.smartFilterForm.get('name')!.setErrors(null);
          }
        } else {
          this.smartFilterForm.get('name')!.setErrors({duplicateName: true});
        }
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }


  close() {
    this.modal.dismiss();
  }

  save() {
    this.smartFilter.update(x => {
      x.name = this.smartFilterForm.get('name')!.value;
      return x;
    });

    this.filterService.renameSmartFilter(this.smartFilter()).subscribe({
      next: () => {
        this.modal.close(modalSaved(this.smartFilter()));
      },
      error: () => {
        this.modal.dismiss();
      }
    });
  }

}
