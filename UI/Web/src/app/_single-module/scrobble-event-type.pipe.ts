import { Pipe, PipeTransform } from '@angular/core';
import {ScrobbleEventType} from "../_models/scrobbling/scrobble-event";

@Pipe({
  name: 'scrobbleEventType',
  standalone: true
})
export class ScrobbleEventTypePipe implements PipeTransform {

  transform(value: ScrobbleEventType): string {
    switch (value) {
      case ScrobbleEventType.ChapterRead: return 'Reading Progress';
      case ScrobbleEventType.ScoreUpdated: return 'Rating Update';
      case ScrobbleEventType.AddWantToRead: return 'Want To Read: Add';
      case ScrobbleEventType.RemoveWantToRead: return 'Want To Read: Remove';
      case ScrobbleEventType.Review: return 'Review update';
    }
  }

}
