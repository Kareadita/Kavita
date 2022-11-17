import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SideNavCompanionBarComponent } from './side-nav-companion-bar/side-nav-companion-bar.component';
import { SideNavItemComponent } from './side-nav-item/side-nav-item.component';
import { SideNavComponent } from './side-nav/side-nav.component';
import { PipeModule } from '../pipe/pipe.module';
import { CardsModule } from '../cards/cards.module';
import { FormsModule } from '@angular/forms';
import { NgbNavModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { RouterModule } from '@angular/router';
import { LibrarySettingsModalComponent } from './_components/library-settings-modal/library-settings-modal.component';



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
    NgbNavModule
  ],
  exports: [
    SideNavCompanionBarComponent,
    SideNavItemComponent,
    SideNavComponent
  ]
})
export class SidenavModule { }
