import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  inject,
  Input,
  OnInit,
  ViewChild,
  ViewEncapsulation
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {NgbActiveModal, NgbModalModule} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {UserCollection} from 'src/app/_models/collection-tag';
import {ReadingList} from 'src/app/_models/reading-list';
import {CollectionTagService} from 'src/app/_services/collection-tag.service';
import {CommonModule} from "@angular/common";
import {FilterPipe} from "../../../_pipes/filter.pipe";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";

@Component({
  selector: 'app-bulk-add-to-collection',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FilterPipe, NgbModalModule, TranslocoDirective],
  templateUrl: './bulk-add-to-collection.component.html',
  styleUrls: ['./bulk-add-to-collection.component.scss'],
  encapsulation: ViewEncapsulation.None, // This is needed as per the bootstrap modal documentation to get styles to work.
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BulkAddToCollectionComponent implements OnInit, AfterViewInit {

  private readonly modal = inject(NgbActiveModal);
  private readonly collectionService = inject(CollectionTagService);
  private readonly toastr = inject(ToastrService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly MaxItems = 8;

  @Input({required: true}) title!: string;
  /**
   * Series Ids to add to Collection Tag
   */
  @Input() seriesIds: Array<number> = [];
  @ViewChild('title') inputElem!: ElementRef<HTMLInputElement>;

  /**
   * All existing collections sorted by recent use date
   */
  lists: Array<UserCollection> = [];
  loading: boolean = false;
  isCreating: boolean = false;
  listForm: FormGroup = new FormGroup({});

  ngOnInit(): void {

    this.listForm.addControl('title', new FormControl(this.title, []));
    this.listForm.addControl('filterQuery', new FormControl('', []));

    this.loading = true;
    this.cdRef.markForCheck();
    this.collectionService.allCollections(true).subscribe(tags => {
      // Don't allow Smart Collections in
      this.lists = tags.filter(t => t.source === ScrobbleProvider.Kavita);
      this.loading = false;
      this.cdRef.markForCheck();
    });
  }

  ngAfterViewInit() {
    // Shift focus to input
    if (this.inputElem) {
      this.inputElem.nativeElement.select();
      this.cdRef.markForCheck();
    }
  }

  close() {
    this.modal.close();
  }

  create() {
    if (this.isCreating) return;
    const tagName = this.listForm.value.title;
    this.isCreating = true;
    this.cdRef.markForCheck();
    this.collectionService.addByMultiple(0, this.seriesIds, tagName).subscribe(() => {
      this.toastr.success(translate('toasts.series-added-to-collection', {collectionName: tagName}));
      this.isCreating = false;
      this.modal.close();
    });
  }

  addToCollection(tag: UserCollection) {
    if (this.seriesIds.length === 0) return;

    this.collectionService.addByMultiple(tag.id, this.seriesIds, '').subscribe(() => {
      this.toastr.success(translate('toasts.series-added-to-collection', {collectionName: tag.title}));
      this.modal.close();
    });

  }

  filterList = (listItem: ReadingList) => {
    return listItem.title.toLowerCase().indexOf((this.listForm.value.filterQuery || '').toLowerCase()) >= 0;
  }

}
