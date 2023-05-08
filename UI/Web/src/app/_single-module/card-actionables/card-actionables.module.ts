import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardActionablesComponent } from './card-actionables.component';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { PipeModule } from 'src/app/pipe/pipe.module';
import { DynamicListPipe } from './_pipes/dynamic-list.pipe';



@NgModule({
  declarations: [ 
    CardActionablesComponent, 
    DynamicListPipe
  ],
  imports: [
    CommonModule,
    NgbDropdownModule,
  ],
  exports: [ CardActionablesComponent]
})
export class CardActionablesModule { }
