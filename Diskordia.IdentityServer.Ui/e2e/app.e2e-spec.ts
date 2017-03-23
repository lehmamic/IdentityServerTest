import { Diskordia.IdentityServer.UiPage } from './app.po';

describe('diskordia.identity-server.ui App', () => {
  let page: Diskordia.IdentityServer.UiPage;

  beforeEach(() => {
    page = new Diskordia.IdentityServer.UiPage();
  });

  it('should display message saying app works', () => {
    page.navigateTo();
    expect(page.getParagraphText()).toEqual('app works!');
  });
});
