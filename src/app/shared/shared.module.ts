import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RegisterMemberComponent } from './register-member/register-member.component';
import { ReactiveFormsModule } from '@angular/forms';
import { CardItemComponent } from './card-item/card-item.component';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { LibraryCardComponent } from './library-card/library-card.component';



@NgModule({
  declarations: [RegisterMemberComponent, CardItemComponent, LibraryCardComponent],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbDropdownModule
  ],
  exports: [
    RegisterMemberComponent,
    CardItemComponent,
    LibraryCardComponent
  ]
})
export class SharedModule { }
