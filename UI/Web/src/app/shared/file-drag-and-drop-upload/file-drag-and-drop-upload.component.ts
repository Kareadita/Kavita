import {ChangeDetectionStrategy, Component, input, output, signal} from '@angular/core';
import {NgxFileDropEntry, NgxFileDropModule} from "ngx-file-drop";
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, ReactiveFormsModule, Validators} from "@angular/forms";

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
    ReactiveFormsModule
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
  urlControl = new FormControl('', { nonNullable: true, validators: [Validators.required] });

  setMode(mode: UploadMode) {
    this.uploadMode.set(mode);
  }

  handleUrlUpload() {
    const value = this.urlControl.value.trim();
    if (value) {
      this.urlSubmitted.emit(value);
      this.urlControl.reset();
      this.setMode(this.showUrlUpload() ? UploadMode.All : UploadMode.Files);
    }
  }

  protected readonly UploadMode = UploadMode;
}
