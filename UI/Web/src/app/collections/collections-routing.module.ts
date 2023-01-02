import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import { AllCollectionsComponent } from './_components/all-collections/all-collections.component';
import { CollectionDetailComponent } from './_components/collection-detail/collection-detail.component';

const routes: Routes = [
  {
    path: '', 
    runGuardsAndResolvers: 'always',
    canActivate: [AuthGuard],
    children: [
        {path: '', component: AllCollectionsComponent, pathMatch: 'full'},
        {path: ':id', component: CollectionDetailComponent},
    ]
  }
];


@NgModule({
  imports: [RouterModule.forChild(routes), ],
  exports: [RouterModule]
})
export class CollectionsRoutingModule { }
