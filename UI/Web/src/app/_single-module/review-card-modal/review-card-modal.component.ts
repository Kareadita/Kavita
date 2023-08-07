import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  Inject,
  Input, ViewChild,
  ViewContainerRef,
  ViewEncapsulation
} from '@angular/core';
import {CommonModule, DOCUMENT} from '@angular/common';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ReactiveFormsModule} from "@angular/forms";
import {UserReview} from "../review-card/user-review";
import {SpoilerComponent} from "../spoiler/spoiler.component";
import {SafeHtmlPipe} from "../../pipe/safe-html.pipe";
import {TranslocoDirective} from "@ngneat/transloco";

@Component({
  selector: 'app-review-card-modal',
  standalone: true,
    imports: [CommonModule, ReactiveFormsModule, SpoilerComponent, SafeHtmlPipe, TranslocoDirective],
  templateUrl: './review-card-modal.component.html',
  styleUrls: ['./review-card-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class ReviewCardModalComponent implements AfterViewInit {

  @Input({required: true}) review!: UserReview;
  @ViewChild('container', { read: ViewContainerRef }) container!: ViewContainerRef;


  constructor(private modal: NgbActiveModal, @Inject(DOCUMENT) private document: Document) {
  }

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
