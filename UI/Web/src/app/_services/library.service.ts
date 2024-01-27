import { HttpClient } from '@angular/common/http';
import {DestroyRef, Injectable} from '@angular/core';
import { of } from 'rxjs';
import {filter, map, tap} from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { JumpKey } from '../_models/jumpbar/jump-key';
import { Library, LibraryType } from '../_models/library/library';
import { DirectoryDto } from '../_models/system/directory-dto';
import {EVENTS, MessageHubService} from "./message-hub.service";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";


@Injectable({
  providedIn: 'root'
})
export class LibraryService {

  baseUrl = environment.apiUrl;

  private libraryNames: {[key:number]: string} | undefined = undefined;
  private libraryTypes: {[key: number]: LibraryType} | undefined = undefined;

  constructor(private httpClient: HttpClient, private readonly messageHub: MessageHubService, private readonly destroyRef: DestroyRef) {
    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef), filter(e => e.event === EVENTS.LibraryModified),
      tap((e) => {
      this.libraryNames = undefined;
    })).subscribe();
  }

  getLibraryNames() {
    if (this.libraryNames != undefined) {
      return of(this.libraryNames);
    }

    return this.httpClient.get<Library[]>(this.baseUrl + 'library').pipe(map(libraries => {
      this.libraryNames = {};
      libraries.forEach(lib => {
        if (this.libraryNames !== undefined) {
          this.libraryNames[lib.id] = lib.name;
        }
      });
      return this.libraryNames;
    }));
  }

  getLibraryName(libraryId: number) {
    if (this.libraryNames != undefined && this.libraryNames.hasOwnProperty(libraryId)) {
      return of(this.libraryNames[libraryId]);
    }
    return this.httpClient.get<Library[]>(this.baseUrl + 'library').pipe(map(l => {
      this.libraryNames = {};
      l.forEach(lib => {
        if (this.libraryNames !== undefined) {
          this.libraryNames[lib.id] = lib.name;
        }
      });
      return this.libraryNames[libraryId];
    }));
  }

  libraryNameExists(name: string) {
    return this.httpClient.get<boolean>(this.baseUrl + 'library/name-exists?name=' + name);
  }

  listDirectories(rootPath: string) {
    let query = '';
    if (rootPath !== undefined && rootPath.length > 0) {
      query = '?path=' + encodeURIComponent(rootPath);
    }

    return this.httpClient.get<DirectoryDto[]>(this.baseUrl + 'library/list' + query);
  }

  getJumpBar(libraryId: number) {
    return this.httpClient.get<JumpKey[]>(this.baseUrl + 'library/jump-bar?libraryId=' + libraryId);
  }

  getLibraries() {
    return this.httpClient.get<Library[]>(this.baseUrl + 'library');
  }

  updateLibrariesForMember(username: string, selectedLibraries: Library[]) {
    return this.httpClient.post(this.baseUrl + 'library/grant-access', {username, selectedLibraries});
  }

  scan(libraryId: number, force = false) {
    return this.httpClient.post(this.baseUrl + 'library/scan?libraryId=' + libraryId + '&force=' + force, {});
  }

  analyze(libraryId: number) {
    return this.httpClient.post(this.baseUrl + 'library/analyze?libraryId=' + libraryId, {});
  }

  refreshMetadata(libraryId: number, forceUpdate = false) {
    return this.httpClient.post(this.baseUrl + 'library/refresh-metadata?libraryId=' + libraryId + '&force=' + forceUpdate, {});
  }

  create(model: {name: string, type: number, folders: string[]}) {
    return this.httpClient.post(this.baseUrl + 'library/create', model);
  }

  delete(libraryId: number) {
    return this.httpClient.delete(this.baseUrl + 'library/delete?libraryId=' + libraryId, {});
  }

  update(model: {name: string, folders: string[], id: number}) {
    return this.httpClient.post(this.baseUrl + 'library/update', model);
  }

  getLibraryType(libraryId: number) {
    if (this.libraryTypes != undefined && this.libraryTypes.hasOwnProperty(libraryId)) {
      return of(this.libraryTypes[libraryId]);
    }
    return this.httpClient.get<LibraryType>(this.baseUrl + 'library/type?libraryId=' + libraryId).pipe(map(l => {
      if (this.libraryTypes === undefined) {
        this.libraryTypes = {};
      }

      this.libraryTypes[libraryId] = l;
      return this.libraryTypes[libraryId];
    }));
  }
}
