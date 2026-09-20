import {ChangeDetectionStrategy, Component, input, output, signal} from '@angular/core';
import {NgxFileDropEntry, NgxFileDropModule} from "ngx-file-drop";
import {TranslocoDirective} from "@jsverse/transloco";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {ValidationErrorsComponent} from "../_components/validation-errors/validation-errors.component";
import {form, FormField, required} from "@angular/forms/signals";

export enum UploadMode {
  All = 0,
  Files = 1,
  Url = 2,
}

@Component({
  selector: 'app-file-drag-and-drop-upload',
  imports: [
    NgxFileDropModule,
    TranslocoDirective,
    FormFieldDirective, ValidationErrorsComponent, FormField
  ],
  templateUrl: './file-drag-and-drop-upload.component.html',
  styleUrl: './file-drag-and-drop-upload.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FileDragAndDropUploadComponent {

  acceptableExtensions = input.required<string>();
  directory = input(false);
  showUrlUpload = input(false);

  uploadText = input<string>();
  urlText = input<string>();

  dropped = output<NgxFileDropEntry[]>();
  urlSubmitted = output<string>();

  uploadMode = signal<UploadMode>(UploadMode.Files);
  formModel = signal('');
  formGroup = form(this.formModel, path => {
    required(path);
  });

  setMode(mode: UploadMode) {
    this.uploadMode.set(mode);
  }

  handleUrlUpload() {
    const value = this.formModel();
    if (value) {
      this.urlSubmitted.emit(value);
      this.formGroup().reset();
      this.setMode(this.showUrlUpload() ? UploadMode.All : UploadMode.Files);
    }
  }

  protected readonly UploadMode = UploadMode;
}
