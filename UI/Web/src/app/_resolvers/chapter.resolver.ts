import {ResolveFn, Router, UrlTree} from "@angular/router";
import {inject} from "@angular/core";
import {catchError, of} from "rxjs";
import {ChapterService} from "../_services/chapter.service";
import {Chapter} from "../_models/chapter";

export const chapterResolver: ResolveFn<Chapter | UrlTree> = (route, state) => {
  const chapterService = inject(ChapterService);
  const router = inject(Router);

  const chapterId = route.parent?.paramMap.get('chapterId') || route.paramMap.get('chapterId');

  if (!chapterId || chapterId === '0') {
    console.error('Chapter ID not found in route params or 0');
    return of(router.parseUrl('/home'));
  }

  return chapterService.getChapterMetadata(parseInt(chapterId, 10)).pipe(
    catchError(() => {
      return of(router.parseUrl('/home'));
    })
  );
};
