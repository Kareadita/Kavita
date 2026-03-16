import {ChangeDetectionStrategy, Component, inject, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageComponent} from "../../image/image.component";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {SafeUrlPipe} from "../../../_pipes/safe-url.pipe";

@Component({
  selector: 'app-carousel-modal',
  imports: [
    TranslocoDirective,
    ImageComponent,
    SafeUrlPipe
  ],
  templateUrl: './preview-image-modal.component.html',
  styleUrl: './preview-image-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PreviewImageModalComponent {
  protected readonly modal = inject(NgbActiveModal);

  title = input.required<string>();
  image = input.required<string>();

}
