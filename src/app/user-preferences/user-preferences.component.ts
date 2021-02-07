import { Component, OnInit } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { PageSplitOption } from '../_models/preferences/page-split-option';
import { ReadingDirection } from '../_models/preferences/reading-direction';
import { ScalingOption } from '../_models/preferences/scaling-option';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';

@Component({
  selector: 'app-user-preferences',
  templateUrl: './user-preferences.component.html',
  styleUrls: ['./user-preferences.component.scss']
})
export class UserPreferencesComponent implements OnInit {

  readingDirections = [{text: 'Left to Right', value: ReadingDirection.LeftToRight}, {text: 'Right to Left', value: ReadingDirection.RightToLeft}];
  scalingOptions = [{text: 'Fit to Height', value: ScalingOption.FitToHeight}, {text: 'Fit to Width', value: ScalingOption.FitToWidth}, {text: 'Original', value: ScalingOption.Original}];
  pageSplitOptions = [{text: 'Right to Left', value: PageSplitOption.SplitRightToLeft}, {text: 'Left to Right', value: PageSplitOption.SplitLeftToRight}, {text: 'No Split', value: PageSplitOption.NoSplit}];

  settingsForm: FormGroup = new FormGroup({});
  user: User | undefined = undefined;

  constructor(private accountService: AccountService, private toastr: ToastrService) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe((user: User) => {
      if (user) {
        this.user = user;
        this.settingsForm.addControl('readingDirection', new FormControl(user.preferences.readingDirection, []));
        this.settingsForm.addControl('scalingOption', new FormControl(user.preferences.scalingOption, []));
        this.settingsForm.addControl('pageSplitOption', new FormControl(user.preferences.pageSplitOption, []));
      }
    });
  }

  resetForm() {
    if (this.user === undefined) { return; }
    this.settingsForm.get('readingDirection')?.setValue(this.user.preferences.readingDirection);
    this.settingsForm.get('scalingOption')?.setValue(this.user.preferences.scalingOption);
    this.settingsForm.get('pageSplitOption')?.setValue(this.user.preferences.pageSplitOption);
  }

  save() {
    const modelSettings = this.settingsForm.value;
    const data = {readingDirection: parseInt(modelSettings.readingDirection, 10), scalingOption: parseInt(modelSettings.scalingOption, 10), pageSplitOption: parseInt(modelSettings.pageSplitOption, 10), hideReadOnDetails: false};
    this.accountService.updatePreferences(data).subscribe((updatedPrefs) => {
      this.toastr.success('Server settings updated');
      if (this.user) {
        this.user.preferences = updatedPrefs;
      }
      this.resetForm();
    });
  }

}
