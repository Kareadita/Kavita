import {ChangeDetectionStrategy, Component, DestroyRef, inject, model, OnInit, signal} from '@angular/core';
import {SmartFilter} from "../../../_models/metadata/v2/smart-filter";
import {TranslocoDirective} from "@jsverse/transloco";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {FilterService} from "../../../_services/filter.service";
import {modalSaved} from "../../../_models/modal/modal-result";
import {FormFieldDirective} from "../../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../../../shared/_components/validation-errors/validation-errors.component";
import {form, FormField, required, validate} from "@angular/forms/signals";

@Component({
  selector: 'app-edit-smart-filter-modal',
  imports: [
    TranslocoDirective,
    SentenceCasePipe,
    FormFieldDirective, ValidationErrorsComponent, FormField],
  templateUrl: './edit-smart-filter-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './edit-smart-filter-modal.component.scss'
})
export class EditSmartFilterModalComponent implements OnInit {

  private readonly modal = inject(NgbActiveModal);
  private readonly filterService = inject(FilterService);
  private readonly destroyRef = inject(DestroyRef);

  formModel = signal({
    name: ''
  });
  formGroup = form(this.formModel, path => {
    required(path.name);
    validate(path.name, ctx => {
      const name = ctx.value();
      const otherExists = this.allFilters().some(f => f.id != this.smartFilter().id && f.name == name);
      if (otherExists) {
        return {
          kind: 'duplicateName'
        };
      }

      return null;
    })
  });

  smartFilter = model.required<SmartFilter>();
  allFilters = signal<SmartFilter[]>([]);

  ngOnInit(): void {
    this.filterService.getAllFilters().subscribe(data => {
      this.allFilters.set(data);
    });

    this.formModel.set({name: this.smartFilter().name})
  }

  close() {
    this.modal.dismiss();
  }

  save() {
    this.smartFilter.update(x => {
      x.name = this.formModel().name;
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
