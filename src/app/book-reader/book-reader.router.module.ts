import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { BookReaderComponent } from './book-reader/book-reader.component';

const routes: Routes = [
  {
      path: ':chapterId',
      component: BookReaderComponent,
  }
];


@NgModule({
    imports: [RouterModule.forChild(routes), ],
    exports: [RouterModule]
})
export class BookReaderRoutingModule { }
