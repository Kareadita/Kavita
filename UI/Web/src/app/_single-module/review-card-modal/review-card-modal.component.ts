import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  viewChild,
  ViewContainerRef,
  ViewEncapsulation
} from '@angular/core';
import {DOCUMENT, NgOptimizedImage} from '@angular/common';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ReactiveFormsModule} from "@angular/forms";
import {UserReview} from "../../_models/user-review";
import {SpoilerComponent} from "../spoiler/spoiler.component";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";

@Component({
  selector: 'app-review-card-modal',
  imports: [ReactiveFormsModule, SafeHtmlPipe, TranslocoDirective, NgOptimizedImage, ProviderImagePipe],
  templateUrl: './review-card-modal.component.html',
  styleUrls: ['./review-card-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None
})
export class ReviewCardModalComponent {

  private readonly document = inject<Document>(DOCUMENT);
  private readonly modal = inject(NgbActiveModal);

  review = input.required<UserReview>();
  container = viewChild.required('container', { read: ViewContainerRef });

  constructor() {
    afterNextRender(() => {
      const spoilers = this.document.querySelectorAll('span.spoiler');

      for (let i = 0; i < spoilers.length; i++) {
        const spoiler = spoilers[i];
        const componentRef = this.container().createComponent<SpoilerComponent>(SpoilerComponent, {
          projectableNodes: [[document.createTextNode('')]]
        });
        componentRef.setInput('html', spoiler.innerHTML);
        if (spoiler.parentNode != null) {
          spoiler.parentNode.replaceChild(componentRef.location.nativeElement, spoiler);
        }
      }
    });
  }

  close() {
    this.modal.dismiss();
  }
}
