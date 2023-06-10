import {Component, Input} from '@angular/core';
import { CommonModule } from '@angular/common';
import {SharedModule} from "../../shared/shared.module";
import {UserReview} from "./user-review";

@Component({
  selector: 'app-review-card',
  standalone: true,
  imports: [CommonModule, SharedModule],
  templateUrl: './review-card.component.html',
  styleUrls: ['./review-card.component.scss']
})
export class ReviewCardComponent {

  @Input({required: true}) review!: UserReview;

}
