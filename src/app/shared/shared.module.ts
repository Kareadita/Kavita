import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RegisterMemberComponent } from './register-member/register-member.component';
import { ReactiveFormsModule } from '@angular/forms';
import { CardItemComponent } from './card-item/card-item.component';
import { NgbCollapseModule, NgbDropdownModule, NgbProgressbarModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { LibraryCardComponent } from './library-card/library-card.component';
import { SeriesCardComponent } from './series-card/series-card.component';
import { CardDetailsModalComponent } from './_modals/card-details-modal/card-details-modal.component';
import { Base64ImageComponent } from './base64-image/base64-image.component';
import { ConfirmDialogComponent } from './confirm-dialog/confirm-dialog.component';
import { SafeHtmlPipe } from './safe-html.pipe';


@NgModule({
  declarations: [
    RegisterMemberComponent,
    CardItemComponent,
    LibraryCardComponent,
    SeriesCardComponent,
    CardDetailsModalComponent,
    Base64ImageComponent,
    ConfirmDialogComponent,
    SafeHtmlPipe
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbProgressbarModule,
    NgbTooltipModule,
    NgbCollapseModule,
  ],
  exports: [
    RegisterMemberComponent,
    CardItemComponent,
    LibraryCardComponent,
    SeriesCardComponent,
    Base64ImageComponent
  ]
})
export class SharedModule { }
