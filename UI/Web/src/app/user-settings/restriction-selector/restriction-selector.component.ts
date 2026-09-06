import {ChangeDetectionStrategy, Component, effect, inject, input, output, signal} from '@angular/core';
import {NgbTooltip} from '@ng-bootstrap/ng-bootstrap';
import {NgTemplateOutlet, TitleCasePipe} from '@angular/common';
import {TranslocoModule} from "@jsverse/transloco";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {MetadataService} from "../../_services/metadata.service";
import {Member} from "../../_models/auth/member";
import {AgeRestriction} from "../../_models/metadata/age-restriction";
import {AgeRatingDto} from "../../_models/metadata/age-rating-dto";
import {AgeRating} from "../../_models/metadata/age-rating";
import {User} from "../../_models/user/user";
import {disabled, form, FormField} from "@angular/forms/signals";

interface FormModel {
  ageRating: string;
  ageRestrictionIncludeUnknowns: boolean;
}

@Component({
  selector: 'app-restriction-selector',
  templateUrl: './restriction-selector.component.html',
  styleUrls: ['./restriction-selector.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgbTooltip, TitleCasePipe, TranslocoModule, NgTemplateOutlet, FormField]
})
export class RestrictionSelectorComponent {
  private readonly metadataService = inject(MetadataService);

  // Inputs/Outputs
  member = input<Member | User | undefined>();
  isAdmin = input(false);
  showContext = input(true);
  resetValue = input<AgeRestriction | undefined>();
  selected = output<AgeRestriction>();

  // State
  ageRatings = signal<AgeRatingDto[]>([]);
  private readonly formModel = signal<FormModel>({
    ageRating: AgeRating.NotApplicable.toString(),
    ageRestrictionIncludeUnknowns: true
  });
  restrictionForm = form(this.formModel, p => {
    disabled(p.ageRating, {
      when: () => this.isAdmin(),
    });
    disabled(p.ageRestrictionIncludeUnknowns, {
      when: () => this.isAdmin()
    });

    disabled(p.ageRestrictionIncludeUnknowns, {
      when: ({valueOf}) => parseInt(valueOf(p.ageRating), 10) as AgeRating === AgeRating.NotApplicable
    });
  });

  constructor() {

    effect(() => {
      const m = this.member();
      if (!m) return;

      this.restrictionForm.ageRating().value.set((m.ageRestriction.ageRating || AgeRating.NotApplicable).toString());
      this.restrictionForm.ageRestrictionIncludeUnknowns().value.set(m.ageRestriction.includeUnknowns);
    });

    effect(() => {
      const r = this.resetValue();
      if (r == null) return;

      this.restrictionForm.ageRating().value.set(r.ageRating.toString());
      this.restrictionForm.ageRestrictionIncludeUnknowns().value.set(r.includeUnknowns);
    });


    // Load age ratings
    this.metadataService.getAllAgeRatings()
      .pipe(takeUntilDestroyed())
      .subscribe(ratings => this.ageRatings.set(ratings));

    effect(() => {
      const formModel = this.formModel();

      this.selected.emit({
        ageRating: parseInt(formModel.ageRating, 10) as AgeRating,
        includeUnknowns: formModel.ageRestrictionIncludeUnknowns
      });
    });
  }
}
