import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SideNavComponent } from './_components/side-nav/side-nav.component';
import { PipeModule } from '../pipe/pipe.module';
import { CardsModule } from '../cards/cards.module';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { NgbNavModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { RouterModule } from '@angular/router';
import { LibrarySettingsModalComponent } from './_modals/library-settings-modal/library-settings-modal.component';
import { SideNavCompanionBarComponent } from './_components/side-nav-companion-bar/side-nav-companion-bar.component';
import { SideNavItemComponent } from './_components/side-nav-item/side-nav-item.component';



@NgModule({
  declarations: [
    SideNavCompanionBarComponent,
    SideNavItemComponent,
    SideNavComponent,
    LibrarySettingsModalComponent
  ],
  imports: [
    CommonModule,
    RouterModule,
    PipeModule,
    CardsModule,
    FormsModule,
    NgbTooltipModule,
    NgbNavModule,
    ReactiveFormsModule
  ],
  exports: [
    SideNavCompanionBarComponent,
    SideNavItemComponent,
    SideNavComponent
  ]
})
export class SidenavModule { }
