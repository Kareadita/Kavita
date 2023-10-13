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


  constructor() {}

  ngOnInit(): void {
    this.formGroup.addControl('host', new FormControl(this.source.host, [Validators.required]));
    this.formGroup.addControl('apiKey', new FormControl(this.source.apiKey, [Validators.required]));
    this.cdRef.markForCheck();

    this.formGroup.get('host')?.valueChanges.pipe(
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef),
      tap((val) => console.log('host value: ', val)),
      switchMap((val) => this.externalSourceService.hostExists(val)),
      tap(isError => this.formGroup.setErrors({notUnique: isError}))
    ).subscribe();
  }

  resetForm() {
    this.formGroup.get('host')?.setValue(this.source.host);
    this.formGroup.get('apiKey')?.setValue(this.source.apiKey);
    this.cdRef.markForCheck();
  }

  saveForm() {
    if (this.source === undefined) return;
    if (this.source.id === 0) {
      // We need to create a new one
      this.externalSourceService.createSource(this.formGroup.value).subscribe((updatedSource) => {
        this.source = {...updatedSource};
        this.sourceUpdate.emit(this.source);
        this.toggleViewMode();
      });
      return;
    }

    this.externalSourceService.updateSource({id: this.source.id, ...this.formGroup.value}).subscribe((updatedSource) => {
      this.source!.host = this.formGroup.value.host;
      this.source!.apiKey = this.formGroup.value.apiKey;

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
