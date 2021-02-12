import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { MangaReaderComponent } from './manga-reader.component';

const routes: Routes = [
  {
      path: ':chapterId',
      component: MangaReaderComponent
  }
];


@NgModule({
    imports: [RouterModule.forChild(routes), ],
    exports: [RouterModule]
})
export class MangaReaderRoutingModule { }
