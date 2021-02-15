import { Injectable } from '@angular/core';
import { Chapter } from 'src/app/_models/chapter';
import { Volume } from 'src/app/_models/volume';

@Injectable({
  providedIn: 'root'
})
export class UtilityService {

  constructor() { }

  sortVolumes = (a: Volume, b: Volume) => {
    if (a === b) { return 0; }
    else if (a.number === 0) { return 1; }
    else if (b.number === 0) { return -1; }
    else {
      return a.number < b.number ? -1 : 1;
    }
  }

  sortChapters = (a: Chapter, b: Chapter) => {
    if (a === b) { return 0; }
    else {
      return parseFloat(a.number) < parseFloat(b.number) ? -1 : 1;
    }
  }

}
