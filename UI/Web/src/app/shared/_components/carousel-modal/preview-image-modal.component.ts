import {ChangeDetectionStrategy, Component, inject, Input} from '@angular/core';
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
  protected readonly modalService = inject(NgbActiveModal);

  @Input({required:true}) title: string = '';
  @Input({required: true}) image: string = '';



}
