import {ChangeDetectionStrategy, Component, inject, model, OnInit, output, signal} from '@angular/core';
import {ExternalSource} from "../../../_models/sidenav/external-source";
import {NgbCollapse} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ExternalSourceService} from "../../../_services/external-source.service";
import {ToastrService} from '@openng/ngx-toastr';
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {form, FormField, required} from "@angular/forms/signals";
import {url} from "../../../_validators/url.validator";
import {ConfirmService} from "../../../shared/confirm.service";
import {filter, switchMap, tap} from "rxjs";

@Component({
    selector: 'app-edit-external-source-item',
  imports: [NgbCollapse, TranslocoDirective, FormFieldDirective, ValidationErrorsComponent, FormField],
    templateUrl: './edit-external-source-item.component.html',
    styleUrls: ['./edit-external-source-item.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class EditExternalSourceItemComponent {

  private readonly externalSourceService = inject(ExternalSourceService);
  private readonly toastr = inject(ToastrService);
  private readonly confirmService = inject(ConfirmService);

  source = model.required<ExternalSource>();
  isViewMode = model<boolean>(true);

  readonly sourceUpdate = output<ExternalSource>();
  readonly sourceDelete = output<ExternalSource>();

  formModel = signal<ExternalSource>({
    apiKey: "", host: "", id: 0, name: ""
  });
  formGroup = form(this.formModel, path => {
    required(path.name);
    required(path.host);
    url(path.host, { requireTls: false });
  });

  resetForm() {
    this.formModel.set(this.source());
  }

  saveForm() {
    if (this.source() === undefined) return;

    const model = this.formGroup().value();
    this.externalSourceService.sourceExists(model.host, model.name, model.apiKey).subscribe(exists => {
      if (exists) {
          this.toastr.error(translate('toasts.external-source-already-exists'));
          return;
      }

      if (this.source().id === 0) {
          // We need to create a new one
          this.externalSourceService.createSource(model).subscribe((updatedSource) => {
              this.source.set({...updatedSource} as ExternalSource);
              this.sourceUpdate.emit(this.source());
              this.toggleViewMode();
          });
          return;
      }

      this.externalSourceService.updateSource(model).subscribe((updatedSource) => {
          this.source.set(updatedSource);
          this.sourceUpdate.emit(this.source());
          this.toggleViewMode();
      });
    });
  }

  delete() {
    if (this.source().id === 0) {
        this.sourceDelete.emit(this.source());
        if (!this.isViewMode()) {
            this.toggleViewMode();
        }
      return;
    }

    this.confirmService.confirm$(translate('edit-external-source-item.confirm-delete', {name: this.source().name})).pipe(
      filter(b => b),
      switchMap(() => this.externalSourceService.deleteSource(this.source().id)),
      tap(() => {
        this.sourceDelete.emit(this.source());
        if (!this.isViewMode()) {
          this.toggleViewMode();
        }
      })
    ).subscribe();
  }

  toggleViewMode() {
    this.isViewMode.update(x => !x);
    if (!this.isViewMode()) {
      this.resetForm();
    }
  }
}
