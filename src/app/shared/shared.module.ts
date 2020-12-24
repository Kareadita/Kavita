import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RegisterMemberComponent } from './register-member/register-member.component';
import { ReactiveFormsModule } from '@angular/forms';



@NgModule({
  declarations: [RegisterMemberComponent],
  imports: [
    CommonModule,
    ReactiveFormsModule
  ],
  exports: [
    RegisterMemberComponent
  ]
})
export class SharedModule { }
