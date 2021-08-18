import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { pageSplitOptions, Preferences, readingDirections, scalingOptions, readingModes } from '../_models/preferences/preferences';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { Options } from '@angular-slider/ngx-slider';
import { BookService } from '../book-reader/book.service';
import { NavService } from '../_services/nav.service';
import { Title } from '@angular/platform-browser';
import { PageBookmark } from '../_models/page-bookmark';
import { ReaderService } from '../_services/reader.service';
import { SeriesService } from '../_services/series.service';
import { Series } from '../_models/series';
import { BookmarksModalComponent } from '../cards/_modals/bookmarks-modal/bookmarks-modal.component';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';

// TODO: Move to own module and lazy load

@Component({
  selector: 'app-user-preferences',
  templateUrl: './user-preferences.component.html',
  styleUrls: ['./user-preferences.component.scss']
})
export class UserPreferencesComponent implements OnInit, OnDestroy {

  readingDirections = readingDirections;
  scalingOptions = scalingOptions;
  pageSplitOptions = pageSplitOptions;
  readingModes = readingModes;

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

  tabs = ['Preferences', 'Bookmarks'];
  active = this.tabs[0];
  bookmarks: Array<PageBookmark> = [];
  series: Array<Series> = [];
  loadingBookmarks: boolean = false;

  constructor(private accountService: AccountService, private toastr: ToastrService, private bookService: BookService, 
    private navService: NavService, private titleService: Title, private readerService: ReaderService, private seriesService: SeriesService,
    private modalService: NgbModal) {
    this.fontFamilies = this.bookService.getFontFamilies();
  }

  ngOnInit(): void {
    this.titleService.setTitle('Kavita - User Preferences');
    this.accountService.currentUser$.pipe(take(1)).subscribe((user: User) => {
      if (user) {
        this.user = user;

        if (this.fontFamilies.indexOf(this.user.preferences.bookReaderFontFamily) < 0) {
          this.user.preferences.bookReaderFontFamily = 'default';
        }
        
        this.settingsForm.addControl('readingDirection', new FormControl(user.preferences.readingDirection, []));
        this.settingsForm.addControl('scalingOption', new FormControl(user.preferences.scalingOption, []));
        this.settingsForm.addControl('pageSplitOption', new FormControl(user.preferences.pageSplitOption, []));
        this.settingsForm.addControl('autoCloseMenu', new FormControl(user.preferences.autoCloseMenu, []));
        this.settingsForm.addControl('readerMode', new FormControl(user.preferences.readerMode, []));
        this.settingsForm.addControl('bookReaderDarkMode', new FormControl(user.preferences.bookReaderDarkMode, []));
        this.settingsForm.addControl('bookReaderFontFamily', new FormControl(user.preferences.bookReaderFontFamily, []));
        this.settingsForm.addControl('bookReaderFontSize', new FormControl(user.preferences.bookReaderFontSize, []));
        this.settingsForm.addControl('bookReaderLineSpacing', new FormControl(user.preferences.bookReaderLineSpacing, []));
        this.settingsForm.addControl('bookReaderMargin', new FormControl(user.preferences.bookReaderMargin, []));
        this.settingsForm.addControl('bookReaderReadingDirection', new FormControl(user.preferences.bookReaderReadingDirection, []));
        this.settingsForm.addControl('bookReaderTapToPaginate', new FormControl(!!user.preferences.siteDarkMode, []));

        this.settingsForm.addControl('siteDarkMode', new FormControl(!!user.preferences.siteDarkMode, []));
      }
    });

    
    this.readerService.getAllBookmarks().pipe(take(1)).subscribe(bookmarks => {
      this.bookmarks = bookmarks;
      const seriesIds: {[id: number]: string} = {};
      this.bookmarks.forEach(bmk => {
        if (!seriesIds.hasOwnProperty(bmk.seriesId)) {
          seriesIds[bmk.seriesId] = '';
        }
      });

      const ids = Object.keys(seriesIds).map(k => parseInt(k, 10));
      this.seriesService.getAllSeriesByIds(ids).subscribe(series => {
        this.series = series;
      });
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
    this.settingsForm.get('autoCloseMenu')?.setValue(this.user.preferences.autoCloseMenu);
    this.settingsForm.get('readerMode')?.setValue(this.user.preferences.readerMode);
    this.settingsForm.get('pageSplitOption')?.setValue(this.user.preferences.pageSplitOption);
    this.settingsForm.get('bookReaderDarkMode')?.setValue(this.user.preferences.bookReaderDarkMode);
    this.settingsForm.get('bookReaderFontFamily')?.setValue(this.user.preferences.bookReaderFontFamily);
    this.settingsForm.get('bookReaderFontSize')?.setValue(this.user.preferences.bookReaderFontSize);
    this.settingsForm.get('bookReaderLineSpacing')?.setValue(this.user.preferences.bookReaderLineSpacing);
    this.settingsForm.get('bookReaderMargin')?.setValue(this.user.preferences.bookReaderMargin);
    this.settingsForm.get('bookReaderTapToPaginate')?.setValue(this.user.preferences.bookReaderTapToPaginate);
    this.settingsForm.get('bookReaderReadingDirection')?.setValue(this.user.preferences.bookReaderReadingDirection);
    this.settingsForm.get('siteDarkMode')?.setValue(this.user.preferences.siteDarkMode);
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
      autoCloseMenu: modelSettings.autoCloseMenu, 
      readerMode: parseInt(modelSettings.readerMode), 
      bookReaderDarkMode: modelSettings.bookReaderDarkMode,
      bookReaderFontFamily: modelSettings.bookReaderFontFamily,
      bookReaderLineSpacing: modelSettings.bookReaderLineSpacing,
      bookReaderFontSize: modelSettings.bookReaderFontSize,
      bookReaderMargin: modelSettings.bookReaderMargin,
      bookReaderTapToPaginate: modelSettings.bookReaderTapToPaginate,
      bookReaderReadingDirection: parseInt(modelSettings.bookReaderReadingDirection, 10),
      siteDarkMode: modelSettings.siteDarkMode
    };
    this.obserableHandles.push(this.accountService.updatePreferences(data).subscribe((updatedPrefs) => {
      this.toastr.success('Server settings updated');
      if (this.user) {
        this.user.preferences = updatedPrefs;

        this.navService.setDarkMode(this.user.preferences.siteDarkMode);
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

  viewBookmarks(series: Series) {
    const bookmarkModalRef = this.modalService.open(BookmarksModalComponent, { scrollable: true, size: 'lg' });
    bookmarkModalRef.componentInstance.series = series;
    bookmarkModalRef.closed.pipe(take(1)).subscribe(() => {
      
    });
  }

  clearBookmarks(series: Series) {
    this.readerService.clearBookmarks(series.id).subscribe(() => {
      const index = this.series.indexOf(series);
      if (index > -1) {
        this.series.splice(index, 1);
      }
      this.toastr.success(series.name + '\'s bookmarks have been removed');
    });
  }

}
