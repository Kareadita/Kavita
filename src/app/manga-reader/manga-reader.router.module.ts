// ,

import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import { LibraryAccessGuard } from '../_guards/library-access.guard';
import { MangaReaderComponent } from './manga-reader.component';

const routes: Routes = [
    {
        path: ':chapterId',
        runGuardsAndResolvers: 'always',
        canActivate: [AuthGuard, LibraryAccessGuard],
        component: MangaReaderComponent
    }
  ];


@NgModule({
    imports: [RouterModule.forChild(routes), ],
    exports: [RouterModule]
  })
  export class MangaReaderRoutingModule { }
