import {HttpEvent, HttpEventType, HttpProgressEvent, HttpResponse} from "@angular/common/http";
import {Observable} from "rxjs";
import {scan} from "rxjs/operators";

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
 * Encapsulates an in progress download of a Blob with progress reporting activated
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
                saver(event.body, parseContentDisposition(event.headers.get('content-disposition') || '', ''))
              }
              return {
                progress: 100,
                state: 'DONE',
                content: event.body,
                filename: parseContentDisposition(event.headers.get('content-disposition') || '', ''),
              }
            }
            return previous;
          },
          {state: 'PENDING', progress: 0, content: null}
        )
      )
  }


/**
 * Parse Content-Disposition header to extract filename, with fallback.
 * Prefers filename*=UTF-8'' (RFC 5987) for non-ASCII filenames.
 */
export function parseContentDisposition(header: string, fallbackName: string): string {
    if (!header) return fallbackName || 'download';
    const tokens = header.split(';');

    if (tokens.length < 2) return fallbackName || 'download';

    // Prefer filename*=UTF-8'' (RFC 5987) for non-ASCII filenames
    const starToken = tokens.find(t => t.trim().toLowerCase().startsWith('filename*='));
    if (starToken) {
      const parts = starToken.trim().split("''");
      if (parts.length === 2 && parts[1]) {
        try {
          const filename = decodeURIComponent(parts[1]);
          if (filename.startsWith('download_') || filename.startsWith('kavita_download_')) {
            const ext = filename.substring(filename.lastIndexOf('.'), filename.length);
            if (fallbackName) return fallbackName + ext;
            return filename.replace('kavita_', '').replace('download_', '');
          }
          return filename;
        } catch { /* fall through to filename= */ }
      }
    }

    const filenameToken = tokens.find(t => t.trim().toLowerCase().startsWith('filename='));
    if (!filenameToken) return fallbackName || 'download';
    let filename = filenameToken.replace(/filename=/i, '').replace(/"/g, '').trim();

    if (filename.startsWith('download_') || filename.startsWith('kavita_download_')) {
      const ext = filename.substring(filename.lastIndexOf('.'), filename.length);
      if (fallbackName) return fallbackName + ext;
      return filename.replace('kavita_', '').replace('download_', '');
    }

    try {
      return decodeURIComponent(filename) || fallbackName || 'download';
    } catch {
      return filename || fallbackName || 'download';
    }
  }
