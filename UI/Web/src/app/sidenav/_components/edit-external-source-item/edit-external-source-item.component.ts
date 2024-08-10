import {ChangeDetectorRef, Component, DestroyRef, EventEmitter, inject, Input, OnInit, Output} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {ExternalSource} from "../../../_models/sidenav/external-source";
import {NgbCollapse} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ExternalSourceService} from "../../../_services/external-source.service";
import {ToastrService} from "ngx-toastr";

@Component({
  selector: 'app-edit-external-source-item',
  standalone: true,
  imports: [NgbCollapse, ReactiveFormsModule, TranslocoDirective],
  templateUrl: './edit-external-source-item.component.html',
  styleUrls: ['./edit-external-source-item.component.scss']
})
export class EditExternalSourceItemComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly externalSourceService = inject(ExternalSourceService);
  private readonly toastr = inject(ToastrService);

  @Input({required: true}) source!: ExternalSource;
  @Output() sourceUpdate = new EventEmitter<ExternalSource>();
  @Output() sourceDelete = new EventEmitter<ExternalSource>();
  @Input() isViewMode: boolean = true;

  formGroup: FormGroup = new FormGroup({});

  ngOnInit(): void {
    this.formGroup.addControl('name', new FormControl(this.source.name, [Validators.required]));
    this.formGroup.addControl('host', new FormControl(this.source.host, [Validators.required, Validators.pattern(/^(http:|https:)+[^\s]+[\w]\/?$/)]));
    this.formGroup.addControl('apiKey', new FormControl(this.source.apiKey, [Validators.required]));
    this.cdRef.markForCheck();
  }

  hasErrors(controlName: string) {
    const errors = this.formGroup.get(controlName)?.errors;
    return Object.values(errors || []).filter(v => v).length > 0;
  }

  resetForm() {
    this.formGroup.get('host')?.setValue(this.source.host);
    this.formGroup.get('name')?.setValue(this.source.name);
    this.formGroup.get('apiKey')?.setValue(this.source.apiKey);
    this.cdRef.markForCheck();
  }

  saveForm() {
    if (this.source === undefined) return;

    const model = this.formGroup.value;
    this.externalSourceService.sourceExists(model.host, model.name, model.apiKey).subscribe(exists => {
      if (exists) {
          this.toastr.error(translate('toasts.external-source-already-exists'));
          return;
      }

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
    });
  }

  delete() {
    if (this.source.id === 0) {
        this.sourceDelete.emit(this.source);
        if (!this.isViewMode) {
            this.toggleViewMode();
        }
      return;
    }

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
