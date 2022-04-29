import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgbAccordionModule, NgbNavModule } from '@ng-bootstrap/ng-bootstrap';
import { CardsModule } from '../cards/cards.module';
import { TypeaheadModule } from '../typeahead/typeahead.module';
import { ThemeTestComponent } from './theme-test/theme-test.component';
import { SharedModule } from '../shared/shared.module';
import { PipeModule } from '../pipe/pipe.module';
import { DevOnlyRoutingModule } from './dev-only-routing.module';
import { FormsModule } from '@angular/forms';

/**
 * This module contains components that aren't meant to ship with main code. They are there to test things out. This module may be deleted in future updates.
 */

@NgModule({
  declarations: [
    ThemeTestComponent
  ],
  imports: [
    CommonModule,
    FormsModule,


    TypeaheadModule,
    CardsModule,
    NgbAccordionModule,
    NgbNavModule, 

    
    SharedModule,
    PipeModule,

    DevOnlyRoutingModule
  ]
})
export class DevOnlyModule { }
