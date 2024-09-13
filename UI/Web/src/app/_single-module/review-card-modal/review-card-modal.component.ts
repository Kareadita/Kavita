import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component, inject,
  Inject,
  Input, ViewChild,
  ViewContainerRef,
  ViewEncapsulation
} from '@angular/core';
import {DOCUMENT, NgOptimizedImage} from '@angular/common';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ReactiveFormsModule} from "@angular/forms";
import {UserReview} from "../review-card/user-review";
import {SpoilerComponent} from "../spoiler/spoiler.component";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";

@Component({
  selector: 'app-review-card-modal',
  standalone: true,
    imports: [ReactiveFormsModule, SpoilerComponent, SafeHtmlPipe, TranslocoDirective, DefaultValuePipe, NgOptimizedImage, ProviderImagePipe],
  templateUrl: './review-card-modal.component.html',
  styleUrls: ['./review-card-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class ReviewCardModalComponent implements AfterViewInit {

  private modal = inject(NgbActiveModal);

  @Input({required: true}) review!: UserReview;
  @ViewChild('container', { read: ViewContainerRef }) container!: ViewContainerRef;


  constructor(@Inject(DOCUMENT) private document: Document) {}

  close() {
    this.modal.close();
  }

  ngAfterViewInit() {
    const spoilers = this.document.querySelectorAll('span.spoiler');

    for (let i = 0; i < spoilers.length; i++) {
      const spoiler = spoilers[i];
      const componentRef = this.container.createComponent<SpoilerComponent>(SpoilerComponent);
      componentRef.instance.html = spoiler.innerHTML;
      if (spoiler.parentNode != null) {
        spoiler.parentNode.replaceChild(componentRef.location.nativeElement, spoiler);
      }
      componentRef.instance.cdRef.markForCheck();
    }
  }


}
