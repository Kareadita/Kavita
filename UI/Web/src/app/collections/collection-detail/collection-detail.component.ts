import { Component, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, ActivatedRoute } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { UpdateFilterEvent } from 'src/app/cards/card-detail-layout/card-detail-layout.component';
import { EditCollectionTagsComponent } from 'src/app/cards/_modals/edit-collection-tags/edit-collection-tags.component';
import { UtilityService } from 'src/app/shared/_services/utility.service';
import { CollectionTag } from 'src/app/_models/collection-tag';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Pagination } from 'src/app/_models/pagination';
import { Series } from 'src/app/_models/series';
import { FilterItem, mangaFormatFilters, SeriesFilter } from 'src/app/_models/series-filter';
import { AccountService } from 'src/app/_services/account.service';
import { Action, ActionFactoryService, ActionItem } from 'src/app/_services/action-factory.service';
import { CollectionTagService } from 'src/app/_services/collection-tag.service';
import { ImageService } from 'src/app/_services/image.service';
import { SeriesService } from 'src/app/_services/series.service';

@Component({
  selector: 'app-collection-detail',
  templateUrl: './collection-detail.component.html',
  styleUrls: ['./collection-detail.component.scss']
})
export class CollectionDetailComponent implements OnInit {

  collectionTag!: CollectionTag;
  tagImage: string = '';
  isLoading: boolean = true;
  collections: CollectionTag[] = [];
  collectionTagName: string = '';
  series: Array<Series> = [];
  seriesPagination!: Pagination;
  collectionTagActions: ActionItem<CollectionTag>[] = [];
  isAdmin: boolean = false;
  filters: Array<FilterItem> = mangaFormatFilters;
  filter: SeriesFilter = {
    mangaFormat: null
  };

  constructor(public imageService: ImageService, private collectionService: CollectionTagService, private router: Router, private route: ActivatedRoute, 
    private seriesService: SeriesService, private toastr: ToastrService, private actionFactoryService: ActionFactoryService, 
    private modalService: NgbModal, private titleService: Title, private accountService: AccountService, private utilityService: UtilityService) {
      this.router.routeReuseStrategy.shouldReuseRoute = () => false;

      this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
        if (user) {
          this.isAdmin = this.accountService.hasAdminRole(user);
        }
      });

      const routeId = this.route.snapshot.paramMap.get('id');
      if (routeId === null) {
        this.router.navigate(['collections']);
        return;
      }
      const tagId = parseInt(routeId, 10);
      this.collectionService.allTags().subscribe(tags => {
        this.collections = tags;
        const matchingTags = this.collections.filter(t => t.id === tagId);
        if (matchingTags.length === 0) {
          this.toastr.error('You don\'t have access to any libraries this tag belongs to or this tag is invalid');
          
          return;
        }
        this.collectionTag = matchingTags[0];
        this.tagImage = this.imageService.randomize(this.imageService.getCollectionCoverImage(this.collectionTag.id));
        this.titleService.setTitle('Kavita - ' + this.collectionTag.title + ' Collection');
        this.loadPage();
      });
  }

  ngOnInit(): void {
    this.collectionTagActions = this.actionFactoryService.getCollectionTagActions(this.handleCollectionActionCallback.bind(this));
  }

  onPageChange(pagination: Pagination) {
    this.router.navigate(['collections', this.collectionTag.id], {replaceUrl: true, queryParamsHandling: 'merge', queryParams: {page: this.seriesPagination.currentPage} });
  }

  loadPage() {
    const page = this.route.snapshot.queryParamMap.get('page');
    if (page != null) {
      if (this.seriesPagination === undefined || this.seriesPagination === null) {
        this.seriesPagination = {currentPage: 0, itemsPerPage: 30, totalItems: 0, totalPages: 1};
      }
      this.seriesPagination.currentPage = parseInt(page, 10);
    }
    // Reload page after a series is updated or first load
    this.seriesService.getSeriesForTag(this.collectionTag.id, this.seriesPagination?.currentPage, this.seriesPagination?.itemsPerPage).subscribe(tags => {
      this.series = tags.result;
      this.seriesPagination = tags.pagination;
      this.isLoading = false;
      window.scrollTo(0, 0);
    });
  }

  updateFilter(data: UpdateFilterEvent) {
    this.filter.mangaFormat = data.filterItem.value;
    if (this.seriesPagination !== undefined && this.seriesPagination !== null) {
      this.seriesPagination.currentPage = 1;
      this.onPageChange(this.seriesPagination);
    } else {
      this.loadPage();
    }
  }

  handleCollectionActionCallback(action: Action, collectionTag: CollectionTag) {
    switch (action) {
      case(Action.Edit):
        this.openEditCollectionTagModal(this.collectionTag);
        break;
      default:
        break;
    }
  }

  openEditCollectionTagModal(collectionTag: CollectionTag) {
    const modalRef = this.modalService.open(EditCollectionTagsComponent, { size: 'lg', scrollable: true });
    modalRef.componentInstance.tag = this.collectionTag;
    modalRef.closed.subscribe((results: {success: boolean, coverImageUpdated: boolean}) => {
      this.loadPage();
      if (results.coverImageUpdated) {
        this.tagImage = this.imageService.randomize(this.imageService.getCollectionCoverImage(collectionTag.id));
      }
    });
  }

}
