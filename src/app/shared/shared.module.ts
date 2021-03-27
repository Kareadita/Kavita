import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { CardItemComponent } from './card-item/card-item.component';
import { NgbCollapseModule, NgbDropdownModule, NgbProgressbarModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { LibraryCardComponent } from './library-card/library-card.component';
import { SeriesCardComponent } from './series-card/series-card.component';
import { CardDetailsModalComponent } from './_modals/card-details-modal/card-details-modal.component';
import { ConfirmDialogComponent } from './confirm-dialog/confirm-dialog.component';
import { SafeHtmlPipe } from './safe-html.pipe';
import { LazyLoadImageModule } from 'ng-lazyload-image';
import { CardActionablesComponent } from './card-item/card-actionables/card-actionables.component';
import { RegisterMemberComponent } from './register-member/register-member.component';


@NgModule({
  declarations: [
    RegisterMemberComponent,
    CardItemComponent,
    LibraryCardComponent,
    SeriesCardComponent,
    CardDetailsModalComponent,
    ConfirmDialogComponent,
    SafeHtmlPipe,
    CardActionablesComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbProgressbarModule,
    NgbTooltipModule,
    NgbCollapseModule,
    LazyLoadImageModule
  ],
  exports: [
    RegisterMemberComponent, // TODO: Move this out and put in normal app
    CardItemComponent,
    LibraryCardComponent,
    SeriesCardComponent, // TODO: Remove this component and use just AppCardComponent
    SafeHtmlPipe,
    CardActionablesComponent
  ]
})
export class SharedModule { }
