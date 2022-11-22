import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { PdfReaderComponent } from './_components/pdf-reader/pdf-reader.component';

const routes: Routes = [
  {
      path: ':chapterId',
      component: PdfReaderComponent,
  }
];


@NgModule({
    imports: [RouterModule.forChild(routes), ],
    exports: [RouterModule]
})
export class PdfReaderRoutingModule { }
