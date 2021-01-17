import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RegisterMemberComponent } from './register-member/register-member.component';
import { ReactiveFormsModule } from '@angular/forms';
import { CardItemComponent } from './card-item/card-item.component';
import { NgbDropdownModule, NgbProgressbarModule } from '@ng-bootstrap/ng-bootstrap';
import { LibraryCardComponent } from './library-card/library-card.component';
import { SeriesCardComponent } from './series-card/series-card.component';



@NgModule({
  declarations: [
    RegisterMemberComponent,
    CardItemComponent,
    LibraryCardComponent,
    SeriesCardComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbProgressbarModule
  ],
  exports: [
    RegisterMemberComponent,
    CardItemComponent,
    LibraryCardComponent,
    SeriesCardComponent
  ]
})
export class SharedModule { }
