import {ChangeDetectorRef, Component, DestroyRef, EventEmitter, inject, Input, OnInit, Output} from '@angular/core';
import { CommonModule } from '@angular/common';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {AccountService} from "../../../_services/account.service";
import {ExternalSource} from "../../../_models/sidenav/external-source";
import {NgbCollapse} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@ngneat/transloco";
import {ExternalSourceService} from "../../../external-source.service";
import {distinctUntilChanged, filter, tap} from "rxjs/operators";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {switchMap} from "rxjs";

@Component({
  selector: 'app-edit-external-source-item',
  standalone: true,
  imports: [CommonModule, NgbCollapse, ReactiveFormsModule, TranslocoDirective],
  templateUrl: './edit-external-source-item.component.html',
  styleUrls: ['./edit-external-source-item.component.scss']
})
export class EditExternalSourceItemComponent implements OnInit {

  @Input({required: true}) source!: ExternalSource;
  @Output() sourceUpdate = new EventEmitter<ExternalSource>();
  @Output() sourceDelete = new EventEmitter<ExternalSource>();
  @Input() isViewMode: boolean = true;

  formGroup: FormGroup = new FormGroup({});
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly externalSourceService = inject(ExternalSourceService);

  hasErrors(controlName: string) {
    const errors = this.formGroup.get(controlName)?.errors;
    return Object.values(errors || []).filter(v => v).length > 0;
  }

  constructor() {}

  ngOnInit(): void {
    this.formGroup.addControl('name', new FormControl(this.source.name, [Validators.required]));
    this.formGroup.addControl('host', new FormControl(this.source.host, [Validators.required, Validators.pattern(/^(http:|https:)+[^\s]+[\w]\/?$/)]));
    this.formGroup.addControl('apiKey', new FormControl(this.source.apiKey, [Validators.required]));
    this.cdRef.markForCheck();

    this.formGroup.valueChanges.pipe(
      tap(val => console.log('value changes: ', val)),
      filter(() => this.formGroup.get('name')?.value && this.formGroup.get('host')?.value),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef),
      switchMap((val) => this.externalSourceService.sourceExists(this.formGroup.get('name')!.value || '', this.formGroup.get('host')!.value || '')),
      tap(isError => {
        if (isError) {
          this.formGroup.get('host')?.setErrors({notUnique: isError });
          this.formGroup.get('name')?.setErrors({notUnique: isError });
        } else {
          // @ts-ignore
          delete this.formGroup.get('host')!.errors['notUnique'];
          // @ts-ignore
          delete this.formGroup.get('name')!.errors['notUnique'];
        }

        console.log(this.formGroup.get('name')?.errors)
        this.cdRef.markForCheck();
      })
    ).subscribe();
  }

  resetForm() {
    this.formGroup.get('host')?.setValue(this.source.host);
    this.formGroup.get('name')?.setValue(this.source.name);
    this.formGroup.get('apiKey')?.setValue(this.source.apiKey);
    this.cdRef.markForCheck();
  }

  saveForm() {
    if (this.source === undefined) return;
    if (this.source.id === 0) {
      // We need to create a new one
      this.externalSourceService.createSource({id: 0, ...this.formGroup.value}).subscribe((updatedSource) => {
        this.source = {...updatedSource};
        this.sourceUpdate.emit(this.source);
        this.toggleViewMode();
      });
      return;
    }

    this.externalSourceService.updateSource({id: this.source.id, ...this.formGroup.value}).subscribe((updatedSource) => {
      this.source!.host = this.formGroup.value.host;
      this.source!.apiKey = this.formGroup.value.apiKey;
      this.source!.name = this.formGroup.value.name;

      this.sourceUpdate.emit(this.source);
      this.toggleViewMode();
    });
  }
  delete() {
    this.externalSourceService.deleteSource(this.source.id).subscribe(() => {
      this.sourceDelete.emit(this.source);
      if (!this.isViewMode) {
        this.toggleViewMode();
      }
    });
  }

  toggleViewMode() {
    this.isViewMode = !this.isViewMode;
    if (!this.isViewMode) {
      this.resetForm();
    }
  }
}
