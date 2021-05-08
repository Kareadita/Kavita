import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { Chapter } from 'src/app/_models/chapter';
import { Series } from 'src/app/_models/series';
import { Volume } from 'src/app/_models/volume';
import { ActionItem } from 'src/app/_services/action-factory.service';
import { ImageService } from 'src/app/_services/image.service';
import { LibraryService } from 'src/app/_services/library.service';

@Component({
  selector: 'app-card-item',
  templateUrl: './card-item.component.html',
  styleUrls: ['./card-item.component.scss']
})
export class CardItemComponent implements OnInit, OnDestroy {

  @Input() imageUrl = '';
  @Input() title = '';
  @Input() actions: ActionItem<any>[] = [];
  @Input() read = 0; // Pages read
  @Input() total = 0; // Total Pages
  @Input() supressLibraryLink = false;
  @Input() entity!: Series | Volume | Chapter; // This is the entity we are representing. It will be returned if an action is executed.
  @Output() clicked = new EventEmitter<string>();

  libraryName: string | undefined = undefined; // Library name item belongs to
  libraryId: number | undefined = undefined; 
  private readonly onDestroy = new Subject<void>();

  constructor(public imageSerivce: ImageService, private libraryService: LibraryService) {
  }

  ngOnInit(): void {
    this.libraryService.getLibraryNames().pipe(takeUntil(this.onDestroy)).subscribe(names => {
      if (this.entity !== undefined && this.entity.hasOwnProperty('libraryId')) {
        this.libraryId = (this.entity as Series).libraryId;
        this.libraryName = names[this.libraryId];
      }
    });
    
  }

  ngOnDestroy() {
    this.onDestroy.next();
  }

  handleClick() {
    this.clicked.emit(this.title);
  }

  isNullOrEmpty(val: string) {
    return val === null || val === undefined || val === '';
  }

  preventClick(event: any) {
    event.stopPropagation();
    event.preventDefault();
  }

  performAction(action: ActionItem<any>) {
    if (typeof action.callback === 'function') {
      action.callback(action.action, this.entity);
    }
  }
}
