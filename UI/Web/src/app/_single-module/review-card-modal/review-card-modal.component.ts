import {ChangeDetectionStrategy, Component, Input, ViewEncapsulation} from '@angular/core';
import {CommonModule} from '@angular/common';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ReactiveFormsModule} from "@angular/forms";
import {PipeModule} from "../../pipe/pipe.module";
import {UserReview} from "../review-card/user-review";

@Component({
  selector: 'app-review-card-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PipeModule],
  templateUrl: './review-card-modal.component.html',
  styleUrls: ['./review-card-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
})
export class ReviewCardModalComponent {

  @Input({required: true}) review!: UserReview;


  constructor(private modal: NgbActiveModal) {
  }

  close() {
    this.modal.close();
  }


}
