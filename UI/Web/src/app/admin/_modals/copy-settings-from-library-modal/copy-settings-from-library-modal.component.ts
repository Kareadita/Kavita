import {ChangeDetectionStrategy, Component, inject, input, signal} from '@angular/core';
import {Library} from "../../../_models/library/library";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {ReactiveFormsModule} from "@angular/forms";
import {form, FormField} from "@angular/forms/signals";

interface LibraryFormModel {
  library: string;
}

@Component({
  selector: 'app-copy-settings-from-library-modal',
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
    FormField,
  ],
  templateUrl: './copy-settings-from-library-modal.component.html',
  styleUrl: './copy-settings-from-library-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CopySettingsFromLibraryModalComponent {
  protected readonly modal = inject(NgbActiveModal);

  libraries = input<Library[]>([]);
  private readonly libraryFormModel = signal<LibraryFormModel>({
    library: ''
  });
  libraryForm = form(this.libraryFormModel);

  save() {
    const raw = this.libraryFormModel().library;
    if (!raw) return;
    this.modal.close(parseInt(raw + '', 10));
  }
}
