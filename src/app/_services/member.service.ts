import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { Member } from '../_models/member';

@Injectable({
  providedIn: 'root'
})
export class MemberService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  getMembers() {
    return this.httpClient.get<Member[]>(this.baseUrl + 'users');
  }

  adminExists() {
    return this.httpClient.get<boolean>(this.baseUrl + 'admin/exists');
  }

  updatePassword(newPassword: string) {
    // TODO: Implement update password (use JWT to assume role)
  }

  deleteMember(member: string) {
    // TODO: Implement delete member (admin only)
  }

}
