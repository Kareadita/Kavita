import { HttpEvent, HttpEventType, HttpHeaders, HttpProgressEvent, HttpResponse } from "@angular/common/http";
  import { Observable } from "rxjs";
  import { scan } from "rxjs/operators";
  
  function isHttpResponse<T>(event: HttpEvent<T>): event is HttpResponse<T> {
    return event.type === HttpEventType.Response;
  }
  
  function isHttpProgressEvent(
    event: HttpEvent<unknown>
  ): event is HttpProgressEvent {
    return (
      event.type === HttpEventType.DownloadProgress ||
      event.type === HttpEventType.UploadProgress
    );
  }

/**
 * Encapsulates an inprogress download of a Blob with progress reporting activated
 */ 
export interface Download {
  content: Blob | null;
  progress: number;
  state: "PENDING" | "IN_PROGRESS" | "DONE";
  filename?: string;
  loaded?: number;
  total?: number
}
  
export function download(saver?: (b: Blob, filename: string) => void): (source: Observable<HttpEvent<Blob>>) => Observable<Download> {
    return (source: Observable<HttpEvent<Blob>>) =>
      source.pipe(
        scan((previous: Download, event: HttpEvent<Blob>): Download => {
            if (isHttpProgressEvent(event)) {
              return {
                progress: event.total
                  ? Math.round((100 * event.loaded) / event.total)
                  : previous.progress,
                state: 'IN_PROGRESS',
                content: null,
                loaded: event.loaded,
                total: event.total
              }
            }
            if (isHttpResponse(event)) {
              if (saver && event.body) {
                saver(event.body, getFilename(event.headers, ''))
              }
              return {
                progress: 100,
                state: 'DONE',
                content: event.body,
                filename: getFilename(event.headers, ''),
              }
            }
            return previous;
          },
          {state: 'PENDING', progress: 0, content: null}
        )
      )
  }


function getFilename(headers: HttpHeaders, defaultName: string) {
    const tokens = (headers.get('content-disposition') || '').split(';');
    let filename = tokens[1].replace('filename=', '').replace(/"/ig, '').trim();  
    if (filename.startsWith('download_') || filename.startsWith('kavita_download_')) {
      const ext = filename.substring(filename.lastIndexOf('.'), filename.length);
      if (defaultName !== '') {
        return defaultName + ext;
      }
      return filename.replace('kavita_', '').replace('download_', '');
      //return defaultName + ext;
    }
    return filename;
  }