import { Routes } from '@angular/router';
import { AuthGuard } from '../_guards/auth.guard';
import { AllCollectionsComponent } from '../collections/_components/all-collections/all-collections.component';
import { CollectionDetailComponent } from '../collections/_components/collection-detail/collection-detail.component';

export const routes: Routes = [
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

