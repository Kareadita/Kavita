import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { MangaReaderComponent } from './_components/manga-reader/manga-reader.component';

const routes: Routes = [
  {
      path: ':chapterId',
      component: MangaReaderComponent
  },
  {
    // This will allow the MangaReader to have a list to use for next/prev chapters rather than natural sort order
    path: ':chapterId/list/:listId',
    component: MangaReaderComponent
}
];


@NgModule({
    imports: [RouterModule.forChild(routes), ],
    exports: [RouterModule]
})
export class MangaReaderRoutingModule { }
