import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SwipeDirective } from './ng-swipe.directive';

// All code in this module is based on https://github.com/aGoncharuks/ag-swipe and may contain further enhancements or bugfixes.

@NgModule({
  declarations: [
    SwipeDirective
  ],
  imports: [
    CommonModule
  ],
  exports: [
    SwipeDirective
  ]
})
export class NgSwipeModule { }
