import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SideNavComponent } from './_components/side-nav/side-nav.component';
import { CardsModule } from '../cards/cards.module';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { NgbNavModule, NgbTooltipModule } from '@ng-bootstrap/ng-bootstrap';
import { RouterModule } from '@angular/router';
import { LibrarySettingsModalComponent } from './_modals/library-settings-modal/library-settings-modal.component';
import { SideNavCompanionBarComponent } from './_components/side-nav-companion-bar/side-nav-companion-bar.component';
import { SideNavItemComponent } from './_components/side-nav-item/side-nav-item.component';
import {CardActionablesComponent} from "../cards/card-item/card-actionables/card-actionables.component";
import {SentenceCasePipe} from "../pipe/sentence-case.pipe";
import {FilterPipe} from "../pipe/filter.pipe";



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
    CardsModule,
    FormsModule,
    NgbTooltipModule,
    NgbNavModule,
    ReactiveFormsModule,
    CardActionablesComponent,
    SentenceCasePipe,
    FilterPipe
  ],
  exports: [
    SideNavCompanionBarComponent,
    SideNavItemComponent,
    SideNavComponent
  ]
})
export class SidenavModule { }
