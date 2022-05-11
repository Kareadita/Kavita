import { Injectable } from '@angular/core';
import { map, merge, ReplaySubject } from 'rxjs';
import { ReadingDirection } from 'src/app/_models/preferences/reading-direction';

@Injectable()
export class BookReaderStateService {

  // TODO: REmove this service
  private immersiveModeSource: ReplaySubject<boolean> = new ReplaySubject(1);
  public immersiveMode$ = this.immersiveModeSource.asObservable();

  private drawerOpenSource: ReplaySubject<boolean> = new ReplaySubject(1);
  public drawerOpen$ = this.drawerOpenSource.asObservable();

  private readingDirectionSource: ReplaySubject<ReadingDirection> = new ReplaySubject(1);
  public readingDirection$ = this.readingDirectionSource.asObservable();

  constructor() { }

  setImmersiveMode(state: boolean) {
    this.immersiveModeSource.next(state);
  }

  setDrawerOpen(state: boolean) {
    this.drawerOpenSource.next(state);
  }

  setReadingDirection(state: ReadingDirection) {
    this.readingDirectionSource.next(state);
  }



}
