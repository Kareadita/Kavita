import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  input,
  OnInit,
  signal,
  viewChild,
  ViewEncapsulation
} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {NgbActiveModal, NgbModalModule} from '@ng-bootstrap/ng-bootstrap';
import {ToastrService} from 'ngx-toastr';
import {UserCollection} from 'src/app/_models/collection-tag';
import {CollectionTagService} from 'src/app/_services/collection-tag.service';

import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProvider} from "../../../_services/scrobbling.service";
import {toSignal} from "@angular/core/rxjs-interop";
import {map} from "rxjs/operators";

@Component({
    selector: 'app-bulk-add-to-collection',
    imports: [ReactiveFormsModule, NgbModalModule, TranslocoDirective],
    templateUrl: './bulk-add-to-collection-modal.component.html',
    styleUrls: ['./bulk-add-to-collection-modal.component.scss'],
    encapsulation: ViewEncapsulation.None, // This is needed as per the bootstrap modal documentation to get styles to work.
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class BulkAddToCollectionModalComponent implements OnInit {

  private readonly modal = inject(NgbActiveModal);
  private readonly collectionService = inject(CollectionTagService);
  private readonly toastr = inject(ToastrService);
  protected readonly MaxItems = 8;

  title = input.required<string>();
  /**
   * Series Ids to add to Collection Tag
   */
  seriesIds = input<number[]>([]);
  inputElem = viewChild('title', {read: ElementRef<HTMLInputElement>});

  /**
   * All existing collections sorted by recent use date
   */
  lists = signal<UserCollection[]>([]);
  loading = signal(false);
  isCreating = signal(false);
  listForm: FormGroup = new FormGroup({
    title: new FormControl('', []),
    filterQuery: new FormControl('', []),
  });

  private filterQuery = toSignal(
    this.listForm.get('filterQuery')!.valueChanges.pipe(map(v => v ?? '')),
    {initialValue: ''}
  );

  filteredLists = computed(() => {
    const query = this.filterQuery().toLowerCase();
    if (!query) return this.lists();
    return this.lists().filter(item => item.title.toLowerCase().includes(query));
  });

  constructor() {
    afterNextRender(() => {
      const inputElm = this.inputElem();
      if (inputElm) {
        inputElm.nativeElement.select();
      }
    });
  }

  ngOnInit(): void {
    this.listForm.get('title')!.setValue(this.title());
    this.loading.set(true);
    this.collectionService.allCollections(true).subscribe(tags => {
      // Don't allow Smart Collections in
      this.lists.set(tags.filter(t => t.source === ScrobbleProvider.Kavita));
      this.loading.set(false);
    });
  }

  close() {
    this.modal.dismiss();
  }

  create() {
    if (this.isCreating()) return;
    const tagName = this.listForm.value.title;
    this.isCreating.set(true);

    this.collectionService.addByMultiple(0, this.seriesIds(), tagName).subscribe(() => {
      this.toastr.success(translate('toasts.series-added-to-collection', {collectionName: tagName}));
      this.isCreating.set(false);
      this.modal.close();
    });
  }

  addToCollection(tag: UserCollection) {
    if (this.seriesIds().length === 0) return;

    this.collectionService.addByMultiple(tag.id, this.seriesIds(), '').subscribe(() => {
      this.toastr.success(translate('toasts.series-added-to-collection', {collectionName: tag.title}));
      this.modal.close();
    });
  }

}
