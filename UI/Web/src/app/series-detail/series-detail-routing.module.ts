import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { SeriesDetailComponent } from './_components/series-detail/series-detail.component';


const routes: Routes = [
    {
        path: '',
        component: SeriesDetailComponent
    }
];


@NgModule({
    imports: [RouterModule.forChild(routes), ],
    exports: [RouterModule]
})
export class SeriesDetailRoutingModule { }
