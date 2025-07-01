import {Component, input} from '@angular/core';
import {NgOptimizedImage} from "@angular/common";
import {UserOwner} from "../../../_models/user";

@Component({
  selector: 'app-owner-icon',
    imports: [
        NgOptimizedImage
    ],
  templateUrl: './owner-icon.component.html',
  styleUrl: './owner-icon.component.scss'
})
export class OwnerIconComponent {

  owner = input.required<UserOwner>();
  size = input<number>(16);

  protected readonly UserOwner = UserOwner;
}
