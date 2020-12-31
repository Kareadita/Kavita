import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RegisterMemberComponent } from './register-member/register-member.component';
import { ReactiveFormsModule } from '@angular/forms';
import { CardItemComponent } from './card-item/card-item.component';



@NgModule({
  declarations: [RegisterMemberComponent, CardItemComponent],
  imports: [
    CommonModule,
    ReactiveFormsModule
  ],
  exports: [
    RegisterMemberComponent,
    CardItemComponent
  ]
})
export class SharedModule { }
