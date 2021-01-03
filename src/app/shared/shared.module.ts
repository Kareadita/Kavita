import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RegisterMemberComponent } from './register-member/register-member.component';
import { ReactiveFormsModule } from '@angular/forms';
import { CardItemComponent } from './card-item/card-item.component';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';



@NgModule({
  declarations: [RegisterMemberComponent, CardItemComponent],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbDropdownModule
  ],
  exports: [
    RegisterMemberComponent,
    CardItemComponent
  ]
})
export class SharedModule { }
