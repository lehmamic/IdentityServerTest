import { ExternalProvider } from './externalProvider';

export interface LoginOptions {
  rememberLogin: string;

  returnUrl: string;

  allowRememberLogin: boolean;

  enableLocalLogin: boolean;

  externalProviders: Array<ExternalProvider>;

  isExternalLoginOnly: boolean;
}
