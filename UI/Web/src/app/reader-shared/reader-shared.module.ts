import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ShortcutsModalComponent } from './_modals/shortcuts-modal/shortcuts-modal.component';
import { NgbModalModule } from '@ng-bootstrap/ng-bootstrap';



@NgModule({
  declarations: [
    ShortcutsModalComponent
  ],
  imports: [
    CommonModule,
    NgbModalModule
  ],
  exports: [
    ShortcutsModalComponent
  ]
})
export class ReaderSharedModule { }
