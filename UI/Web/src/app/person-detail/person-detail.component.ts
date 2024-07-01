import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {ActivatedRoute, Router} from "@angular/router";
import {PersonService} from "../_services/person.service";
import {Observable} from "rxjs";
import {Person} from "../_models/metadata/person";
import {AsyncPipe} from "@angular/common";
import {ImageComponent} from "../shared/image/image.component";
import {ImageService} from "../_services/image.service";

@Component({
  selector: 'app-person-detail',
  standalone: true,
  imports: [
    AsyncPipe,
    ImageComponent
  ],
  templateUrl: './person-detail.component.html',
  styleUrl: './person-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PersonDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly personService = inject(PersonService);
  protected readonly imageService = inject(ImageService);

  personId!: number;
  person$: Observable<Person> | null = null;

  constructor() {
    const routeId = this.route.snapshot.paramMap.get('personId');
    if (routeId === null) {
      this.router.navigateByUrl('/home');
      return;
    }
    this.personId = parseInt(routeId, 10);

    this.person$ = this.personService.get(this.personId);

    console.log('PersonId: ', routeId);
  }
}
