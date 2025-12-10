import {Routes} from '@angular/router';
import {BookReaderComponent} from '../book-reader/_components/book-reader/book-reader.component';
import {readingProfileResolver} from "../_resolvers/reading-profile.resolver";

export const routes: Routes = [
  {
      path: ':chapterId',
      component: BookReaderComponent,
      resolve: {
        readingProfile: readingProfileResolver
      }
  }
];

