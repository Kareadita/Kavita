import {
  AfterViewInit,
  ChangeDetectorRef,
  Component, computed,
  ElementRef,
  inject,
  Input,
  model,
  OnInit, signal, TemplateRef, viewChild,
  ViewChild
} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ToastrService} from "ngx-toastr";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ReadingProfileService} from "../../../_services/reading-profile.service";
import {ReadingProfile, ReadingProfileKind} from "../../../_models/preferences/reading-profiles";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {SentenceCasePipe} from "../../../_pipes/sentence-case.pipe";
import {ListSelectModalComponent} from "../../../shared/_components/list-select-modal/list-select-modal.component";
import {ClientDevice} from "../../../_models/client-device";
import {DeviceService} from "../../../_services/device.service";
import {forkJoin} from "rxjs";

@Component({
  selector: 'app-bulk-set-reading-profile-modal',
  imports: [
    ReactiveFormsModule,
    TranslocoDirective,
    ListSelectModalComponent,
    SentenceCasePipe
  ],
  templateUrl: './bulk-set-reading-profile-modal.component.html',
  styleUrl: './bulk-set-reading-profile-modal.component.scss'
})
export class BulkSetReadingProfileModalComponent implements OnInit {

  private readonly modal = inject(NgbActiveModal);
  private readonly readingProfileService = inject(ReadingProfileService);
  private readonly toastr = inject(ToastrService);
  private readonly deviceService = inject(DeviceService);

  itemTemplate = viewChild.required<TemplateRef<any>>('listItemTemplate');

  /**
   * Series Ids to add to Reading Profile
   */
  @Input() seriesIds: Array<number> = [];
  @Input() libraryId: number | undefined;

  private profiles = signal<ReadingProfile[]>([]);
  private currentlyBoundProfiles = signal<ReadingProfile[]>([]);
  protected devices = signal<ClientDevice[]>([]);
  protected loading = signal(true);

  private  profileMap = computed(() =>
    new Map(this.profiles().map(p => [p.id, p])));
  protected listItems = computed(() =>
    this.profiles().map(profile => ({label: profile.name, value: profile.id})));
  protected preSelectedListItems = computed(() =>
    this.currentlyBoundProfiles().map(profile => profile.id));

  ngOnInit(): void {
    if (this.libraryId !== undefined) {
      this.readingProfileService.getForLibrary(this.libraryId).subscribe(profiles => {
        this.currentlyBoundProfiles.set(profiles)
      });
    } else if (this.seriesIds.length === 1) {
      this.readingProfileService.getAllForSeries(this.seriesIds[0]).subscribe(profiles => {
        this.currentlyBoundProfiles.set(profiles);
      });
    }

    forkJoin([
      this.readingProfileService.getAllProfiles(),
      this.deviceService.getMyClientDevices(),
    ]).subscribe(([profiles, devices]) => {
      this.loading.set(false);
      this.profiles.set(profiles);
      this.devices.set(devices);
    });
  }

  profileDevice(profileId: number) {
    const profile = this.profileMap().get(profileId)!;

    return this.devices().filter(device => profile.deviceIds.includes(device.id));
  }

  validItem(item: number, selection: number[]) {
    const profile = this.profileMap().get(item)!;
    const selectedProfiles = selection.map(id => this.profileMap().get(id)!);

    const selectedDevices = new Set(selectedProfiles.flatMap(profile => profile.deviceIds));

    return !profile.deviceIds.some(id => selectedDevices.has(id));
  }

  validSelection(profileIds: number[]) {
    const profiles = (Array.isArray(profileIds) ? profileIds : [profileIds])
      .map(id => this.profileMap().get(id)!);

    const seen = new Set<number>();

    for (const profile of profiles) {
      for (const deviceId of profile.deviceIds) {
        if (seen.has(deviceId)) {
          return false;
        }

        seen.add(deviceId);
      }
    }

    return true;
  }

  addToProfile(profileIds: number | number[]) {
    profileIds = Array.isArray(profileIds) ? profileIds : [profileIds];

    if (this.seriesIds.length == 1) {
      this.readingProfileService.addToSeries(profileIds, this.seriesIds[0]).subscribe(() => {
        this.toastr.success(translate('toasts.series-bound-to-reading-profile', {amount: profileIds.length}));
        this.modal.close();
      });
      return;
    }

    if (this.seriesIds.length > 1) {
      this.readingProfileService.bulkAddToSeries(profileIds, this.seriesIds).subscribe(() => {
        this.toastr.success(translate('toasts.series-bound-to-reading-profile', {amount: profileIds.length}));
        this.modal.close();
      });
      return;
    }

    if (this.libraryId) {
      this.readingProfileService.addToLibrary(profileIds, this.libraryId).subscribe(() => {
        this.toastr.success(translate('toasts.library-bound-to-reading-profile', {amount: profileIds.length}));
        this.modal.close();
      });
    }
  }

  protected readonly ReadingProfileKind = ReadingProfileKind;
}
