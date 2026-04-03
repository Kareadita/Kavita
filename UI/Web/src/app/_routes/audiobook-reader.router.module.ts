import {Routes} from '@angular/router';
import {AudiobookReaderComponent} from '../audiobook-reader/_components/audiobook-reader/audiobook-reader.component';

export const routes: Routes = [
  {
    path: ':chapterId',
    component: AudiobookReaderComponent
  }
];
