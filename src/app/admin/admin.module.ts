import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminRoutingModule } from './admin-routing.module';
import { UsersComponent } from './users/users.component';
import { ToastrModule } from 'ngx-toastr';



@NgModule({
  declarations: [UsersComponent],
  imports: [
    CommonModule,
    AdminRoutingModule
  ],
  providers: []
})
export class AdminModule { }
