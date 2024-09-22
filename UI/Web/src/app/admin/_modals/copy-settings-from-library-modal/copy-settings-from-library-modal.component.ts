import {ChangeDetectionStrategy, Component, inject, Input} from '@angular/core';
import {Library} from "../../../_models/library/library";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";

@Component({
  selector: 'app-copy-settings-from-library-modal',
  standalone: true,
  imports: [
    TranslocoDirective,
    ReactiveFormsModule,
  ],
  templateUrl: './copy-settings-from-library-modal.component.html',
  styleUrl: './copy-settings-from-library-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CopySettingsFromLibraryModalComponent {
  protected readonly modal = inject(NgbActiveModal);

  @Input() libraries: Array<Library> = [];

  libForm = new FormGroup({
    'library': new FormControl(null),
  });

  save() {
    this.modal.close(parseInt(this.libForm.get('library')?.value + '', 10));
  }
}
