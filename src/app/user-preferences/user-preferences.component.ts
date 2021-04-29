import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { PageSplitOption } from '../_models/preferences/page-split-option';
import { Preferences } from '../_models/preferences/preferences';
import { ReadingDirection } from '../_models/preferences/reading-direction';
import { ScalingOption } from '../_models/preferences/scaling-option';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { Options } from '@angular-slider/ngx-slider';
import { BookService } from '../book-reader/book.service';

@Component({
  selector: 'app-user-preferences',
  templateUrl: './user-preferences.component.html',
  styleUrls: ['./user-preferences.component.scss']
})
export class UserPreferencesComponent implements OnInit, OnDestroy {

  readingDirections = [{text: 'Left to Right', value: ReadingDirection.LeftToRight}, {text: 'Right to Left', value: ReadingDirection.RightToLeft}];
  scalingOptions = [{text: 'Automatic', value: ScalingOption.Automatic}, {text: 'Fit to Height', value: ScalingOption.FitToHeight}, {text: 'Fit to Width', value: ScalingOption.FitToWidth}, {text: 'Original', value: ScalingOption.Original}];
  pageSplitOptions = [{text: 'Right to Left', value: PageSplitOption.SplitRightToLeft}, {text: 'Left to Right', value: PageSplitOption.SplitLeftToRight}, {text: 'No Split', value: PageSplitOption.NoSplit}];

  settingsForm: FormGroup = new FormGroup({});
  passwordChangeForm: FormGroup = new FormGroup({});
  user: User | undefined = undefined;

  passwordsMatch = false;
  resetPasswordErrors: string[] = [];

  obserableHandles: Array<any> = [];

  bookReaderLineSpacingOptions: Options = {
    floor: 100,
    ceil: 250,
    step: 10,
  };
  bookReaderMarginOptions: Options = {
    floor: 0,
    ceil: 30,
    step: 5,
  };
  bookReaderFontSizeOptions: Options = {
    floor: 50,
    ceil: 300,
    step: 10,
  };
  fontFamilies: Array<string> = [];

  constructor(private accountService: AccountService, private toastr: ToastrService, private bookService: BookService) {
    this.fontFamilies = this.bookService.getFontFamilies();
  }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(take(1)).subscribe((user: User) => {
      if (user) {
        this.user = user;

        this.settingsForm.addControl('readingDirection', new FormControl(user.preferences.readingDirection, []));
        this.settingsForm.addControl('scalingOption', new FormControl(user.preferences.scalingOption, []));
        this.settingsForm.addControl('pageSplitOption', new FormControl(user.preferences.pageSplitOption, []));
        this.settingsForm.addControl('bookReaderDarkMode', new FormControl(user.preferences.bookReaderDarkMode, []))
        this.settingsForm.addControl('bookReaderFontFamily', new FormControl(user.preferences.bookReaderFontFamily, []))
        this.settingsForm.addControl('bookReaderFontSize', new FormControl(user.preferences.bookReaderFontSize, []))
        this.settingsForm.addControl('bookReaderLineSpacing', new FormControl(user.preferences.bookReaderLineSpacing, []))
        this.settingsForm.addControl('bookReaderMargin', new FormControl(user.preferences.bookReaderMargin, []))
      }
    });

    this.passwordChangeForm.addControl('password', new FormControl('', [Validators.required]));
    this.passwordChangeForm.addControl('confirmPassword', new FormControl('', [Validators.required]));

    this.obserableHandles.push(this.passwordChangeForm.valueChanges.subscribe(() => {
      const values = this.passwordChangeForm.value;
      this.passwordsMatch = values.password === values.confirmPassword;
    }));
  }

  ngOnDestroy() {
    this.obserableHandles.forEach(o => o.unsubscribe());
  }

  public get password() { return this.passwordChangeForm.get('password'); }
  public get confirmPassword() { return this.passwordChangeForm.get('confirmPassword'); }

  resetForm() {
    if (this.user === undefined) { return; }
    this.settingsForm.get('readingDirection')?.setValue(this.user.preferences.readingDirection);
    this.settingsForm.get('scalingOption')?.setValue(this.user.preferences.scalingOption);
    this.settingsForm.get('pageSplitOption')?.setValue(this.user.preferences.pageSplitOption);
    this.settingsForm.get('bookReaderDarkMode')?.setValue(this.user.preferences.bookReaderDarkMode);
    this.settingsForm.get('bookReaderFontFamily')?.setValue(this.user.preferences.bookReaderFontFamily);
    this.settingsForm.get('bookReaderFontSize')?.setValue(this.user.preferences.bookReaderFontSize);
    this.settingsForm.get('bookReaderLineSpacing')?.setValue(this.user.preferences.bookReaderLineSpacing);
    this.settingsForm.get('bookReaderMargin')?.setValue(this.user.preferences.bookReaderMargin);
  }

  resetPasswordForm() {
    this.passwordChangeForm.get('password')?.setValue('');
    this.passwordChangeForm.get('confirmPassword')?.setValue('');
    this.resetPasswordErrors = [];
  }

  save() {
    if (this.user === undefined) return;
    const modelSettings = this.settingsForm.value;
    const data: Preferences = {
      readingDirection: parseInt(modelSettings.readingDirection, 10), 
      scalingOption: parseInt(modelSettings.scalingOption, 10), 
      pageSplitOption: parseInt(modelSettings.pageSplitOption, 10), 
      bookReaderDarkMode: modelSettings.bookReaderDarkMode,
      bookReaderFontFamily: modelSettings.bookReaderFontFamily,
      bookReaderLineSpacing: modelSettings.bookReaderLineSpacing,
      bookReaderFontSize: modelSettings.bookReaderFontSize,
      bookReaderMargin: modelSettings.bookReaderMargin
    };
    this.obserableHandles.push(this.accountService.updatePreferences(data).subscribe((updatedPrefs) => {
      this.toastr.success('Server settings updated');
      if (this.user) {
        this.user.preferences = updatedPrefs;
      }
      this.resetForm();
    }));
  }

  savePasswordForm() {
    if (this.user === undefined) { return; }

    const model = this.passwordChangeForm.value;
    this.resetPasswordErrors = [];
    this.obserableHandles.push(this.accountService.resetPassword(this.user?.username, model.confirmPassword).subscribe(() => {
      this.toastr.success('Password has been updated');
      this.resetPasswordForm();
    }, err => {
      this.resetPasswordErrors = err;
    }));
  }

}
