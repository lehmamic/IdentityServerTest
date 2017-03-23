import { Component, OnInit } from '@angular/core';
import { Http } from '@angular/http';
import { ActivatedRoute } from '@angular/router';

import { Observable } from 'rxjs/Rx';
import 'rxjs/add/operator/mergeMap';
import 'rxjs/add/operator/map';
import 'rxjs/add/operator/catch';

import { LoginOptions } from './loginOptions';
import { ExternalProvider } from './externalProvider';
import { LocalLogin } from './localLogin';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {
  public options: Observable<LoginOptions>;
  public localLogin: LocalLogin = {
    username: '',
    password: '',
    rememberLogin: false
  };

  constructor(private route: ActivatedRoute, private http: Http) {
    const self = this;
    this.options = this.route.queryParams
      .flatMap((p) => {
        const returnUrl = p['returnUrl'];
        return this.http.get(`http://localhost:5000/api/account/login?returnUrl=${returnUrl}`);
      })
      .map(r => {
        console.log('test ' + r.text());
        const obj: any = r.json();
        return <LoginOptions>obj;
      })
      .catch(e => {
        console.log(e);
        return e;
    });

    // TODO: Redirect directly if the isOnlyExternalLogin property returns true
  }

  ngOnInit() {

  }

  public createExternalLoginUrl(authenticationScheme: string, returnUrl: string): string {
    return `http://localhost:5000/account/externallogin?provider=${authenticationScheme}&returnUrl=${returnUrl}`;
  }

  public onLogin(): void {
    console.log(this.localLogin);
  }
}
