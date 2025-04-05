import {ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {TranslocoDirective} from "@jsverse/transloco";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {FilterService} from "../../../_services/filter.service";
import {debounceTime, distinctUntilChanged, switchMap} from "rxjs/operators";
import {of, tap} from "rxjs";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-edit-smart-filter-modal',
  imports: [
    TranslocoDirective,
    SentenceCasePipe,
    ReactiveFormsModule
  ],
  templateUrl: './edit-smart-filter-modal.component.html',
  styleUrl: './edit-smart-filter-modal.component.scss'
})
export class EditSmartFilterModalComponent implements OnInit {

  private readonly modal = inject(NgbActiveModal);
  private readonly filterService = inject(FilterService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  @Input({required: true}) smartFilter!: SmartFilter;
  @Input({required: true}) allFilters!: SmartFilter[];

  smartFilterForm: FormGroup = new FormGroup({
    'name': new FormControl('', [Validators.required]),
  });

  ngOnInit(): void {
    this.smartFilterForm.get('name')!.setValue(this.smartFilter.name);

    this.smartFilterForm.get('name')!.valueChanges.pipe(
      debounceTime(100),
      distinctUntilChanged(),
      switchMap(name => {
        const other = this.allFilters.find(f => {
          return f.id !== this.smartFilter.id && f.name === name;
        })
        return of(other !== undefined)
      }),
      tap((exists) => {
        const isThisSmartFilter = this.smartFilter.name === this.smartFilterForm.get('name')!.value;
        const empty = (this.smartFilterForm.get('name')!.value as string).trim().length === 0;

        if (!exists || isThisSmartFilter) {
          if (!empty) {
            this.smartFilterForm.get('name')!.setErrors(null);
          }
        } else {
          this.smartFilterForm.get('name')!.setErrors({duplicateName: true});
        }

        this.cdRef.markForCheck();
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe();
  }


  close(closeVal: boolean = false) {
    this.modal.close(closeVal);
  }

  save() {
    this.smartFilter.name = this.smartFilterForm.get('name')!.value;
    this.filterService.renameSmartFilter(this.smartFilter).subscribe({
      next: () => {
        this.modal.close(true);
      },
      error: () => {
        this.modal.close(false);
      }
    });
  }

}
