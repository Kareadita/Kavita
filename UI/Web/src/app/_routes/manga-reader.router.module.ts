import {Routes} from '@angular/router';
import {MangaReaderComponent} from '../manga-reader/_components/manga-reader/manga-reader.component';
import {ReadingProfileResolver} from "../_resolvers/reading-profile.resolver";

export const routes: Routes = [
  {
      path: ':chapterId',
      component: MangaReaderComponent,
      resolve: {
        readingProfile: ReadingProfileResolver
      }
  },
  {
    // This will allow the MangaReader to have a list to use for next/prev chapters rather than natural sort order
    path: ':chapterId/list/:listId',
    component: MangaReaderComponent,
    resolve: {
      readingProfile: ReadingProfileResolver
    }
  }
];

