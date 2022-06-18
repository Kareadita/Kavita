import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ShorcutsModalComponent } from './_modals/shorcuts-modal/shorcuts-modal.component';
import { NgbModalModule } from '@ng-bootstrap/ng-bootstrap';



@NgModule({
  declarations: [
    ShorcutsModalComponent
  ],
  imports: [
    CommonModule,
    NgbModalModule
  ],
  exports: [
    ShorcutsModalComponent
  ]
})
export class ReaderSharedModule { }
