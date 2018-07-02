import { Component, OnInit } from '@angular/core';
import { Http, Headers } from '@angular/http';
import { ActivatedRoute } from '@angular/router';

import { Observable } from 'rxjs/Rx';
import 'rxjs/add/operator/mergeMap';
import 'rxjs/add/operator/map';
import 'rxjs/add/operator/catch';
import 'rxjs/add/operator/do';

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
    rememberLogin: false,
    returnUrl: ''
  };

  constructor(private route: ActivatedRoute, private http: Http) {
    const self = this;
    this.options = this.route.queryParams
      .flatMap((p) => {
        // const returnUrl = encodeURI(p['returnUrl']);
        let returnUrl = '';
        const pattern = 'returnUrl=';
        const input = window.location.href;

        if (input.indexOf(pattern) >= 0) {
          returnUrl = input.substr(input.indexOf(pattern) + pattern.length, input.length);
        }

        return this.http.get(`http://localhost:5000/api/account/login?returnUrl=${returnUrl}`);
      })
      .map(res => {
        console.log('test ' + res.text());
        const obj: any = res.json();
        return <LoginOptions>obj;
      })
      .do(opt => {
        self.localLogin.returnUrl = opt.returnUrl;

        if (opt.isExternalLoginOnly) {
          const provider = opt.externalProviders[0];
          window.location.href =
          `http://localhost:5000/account/externallogin?provider=${provider.authenticationScheme}&returnurl=${opt.returnUrl}`;
        }
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
    const payload: string = JSON.stringify({
      Password: this.localLogin.password,
      Username: this.localLogin.username,
      RememberLogin: this.localLogin.rememberLogin,
      ReturnUrl: this.localLogin.returnUrl
    });
    console.log(payload);
    const headers = new Headers(
    {
      'Content-Type': 'application/json'
    });

    this.http.post(`http://localhost:5000/api/account/login`, payload, { headers: headers})
    .subscribe(res => {
      if (res.status === 200) {
        const data: any = res.json();
        window.location.href = data.returnUrl;
      }},
      e => {
        console.log(e);
      });
  }
}
