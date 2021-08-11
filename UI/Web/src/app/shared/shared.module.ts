import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { CardItemComponent } from './card-item/card-item.component';
import { NgbCollapseModule, NgbDropdownModule, NgbPaginationModule, NgbProgressbarModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { CardDetailsModalComponent } from './_modals/card-details-modal/card-details-modal.component';
import { ConfirmDialogComponent } from './confirm-dialog/confirm-dialog.component';
import { SafeHtmlPipe } from './safe-html.pipe';
import { LazyLoadImageModule } from 'ng-lazyload-image';
import { CardActionablesComponent } from './card-item/card-actionables/card-actionables.component';
import { RegisterMemberComponent } from '../register-member/register-member.component';
import { ReadMoreComponent } from './read-more/read-more.component';
import { RouterModule } from '@angular/router';
import { DrawerComponent } from './drawer/drawer.component';
import { TagBadgeComponent } from './tag-badge/tag-badge.component';
import { CardDetailLayoutComponent } from './card-detail-layout/card-detail-layout.component';
import { ShowIfScrollbarDirective } from './show-if-scrollbar.directive';
import { A11yClickDirective } from './a11y-click.directive';
import { SeriesFormatComponent } from './series-format/series-format.component';
import { UpdateNotificationModalComponent } from './update-notification/update-notification-modal.component';
import { SAVER, getSaver } from './_providers/saver.provider';
import { CircularLoaderComponent } from './circular-loader/circular-loader.component';
import { NgCircleProgressModule } from 'ng-circle-progress';

@NgModule({
  declarations: [
    RegisterMemberComponent,
    CardItemComponent,
    CardDetailsModalComponent,
    ConfirmDialogComponent,
    SafeHtmlPipe,
    CardActionablesComponent,
    ReadMoreComponent,
    DrawerComponent,
    TagBadgeComponent,
    CardDetailLayoutComponent,
    ShowIfScrollbarDirective,
    A11yClickDirective,
    SeriesFormatComponent,
    UpdateNotificationModalComponent,
    CircularLoaderComponent
  ],
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgbProgressbarModule,
    NgbTooltipModule,
    NgbCollapseModule,
    LazyLoadImageModule,
    NgbPaginationModule, // CardDetailLayoutComponent
    NgCircleProgressModule.forRoot()
  ],
  exports: [
    RegisterMemberComponent,
    CardItemComponent,
    SafeHtmlPipe,
    CardActionablesComponent,
    ReadMoreComponent,
    DrawerComponent,
    TagBadgeComponent,
    CardDetailLayoutComponent,
    ShowIfScrollbarDirective,
    A11yClickDirective,
    SeriesFormatComponent,
  ],
  providers: [{provide: SAVER, useFactory: getSaver}]
})
export class SharedModule { }
