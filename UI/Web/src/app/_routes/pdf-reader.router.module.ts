import {Routes} from '@angular/router';
import {PdfReaderComponent} from '../pdf-reader/_components/pdf-reader/pdf-reader.component';
import {ReadingProfileResolver} from "../_resolvers/reading-profile.resolver";

export const routes: Routes = [
  {
      path: ':chapterId',
      component: PdfReaderComponent,
      resolve: {
        readingProfile: ReadingProfileResolver
      }
  }
];
