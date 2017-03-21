// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System.Linq;
using System.Threading.Tasks;
using Diskordia.IdentityServer.Models.Consent;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Microsoft.Extensions.Logging;

namespace Diskordia.IdentityServer.Controllers
{
    public class ConsentService
    {
        private readonly IClientStore clientStore;
        private readonly IResourceStore resourceStore;
        private readonly IIdentityServerInteractionService interaction;
        private readonly ILogger logger;

        public ConsentService(
            IIdentityServerInteractionService interaction,
            IClientStore clientStore,
            IResourceStore resourceStore,
            ILogger logger)
        {
            this.interaction = interaction;
            this.clientStore = clientStore;
            this.resourceStore = resourceStore;
            this.logger = logger;
        }

        public async Task<ProcessConsentResult> ProcessConsent(ConsentInputModel model)
        {
            var result = new ProcessConsentResult();

            ConsentResponse grantedConsent = null;

            // user clicked 'no' - send back the standard 'access_denied' response
            if (model.Button == "no")
            {
                grantedConsent = ConsentResponse.Denied;
            }
            // user clicked 'yes' - validate the data
            else if (model.Button == "yes" && model != null)
            {
                // if the user consented to some scope, build the response model
                if (model.ScopesConsented != null && model.ScopesConsented.Any())
                {
                    var scopes = model.ScopesConsented;
                    if (ConsentOptions.EnableOfflineAccess == false)
                    {
                        scopes = scopes.Where(x => x != IdentityServerConstants.StandardScopes.OfflineAccess);
                    }

                    grantedConsent = new ConsentResponse
                    {
                        RememberConsent = model.RememberConsent,
                        ScopesConsented = scopes.ToArray()
                    };
                }
                else
                {
                    result.ValidationError = ConsentOptions.MuchChooseOneErrorMessage;
                }
            }
            else
            {
                result.ValidationError = ConsentOptions.InvalidSelectionErrorMessage;
            }

            if (grantedConsent != null)
            {
                // validate return url is still valid
                var request = await this.interaction.GetAuthorizationContextAsync(model.ReturnUrl);
                if (result == null)
                {
                  return result;
                }

                // communicate outcome of consent back to identityserver
                await this.interaction.GrantConsentAsync(request, grantedConsent);

                // indiate that's it ok to redirect back to authorization endpoint
                result.RedirectUri = model.ReturnUrl;
            }
            else
            {
                // we need to redisplay the consent UI
                result.ViewModel = await this.BuildViewModelAsync(model.ReturnUrl, model);
            }

            return result;
        }

        public async Task<ConsentViewModel> BuildViewModelAsync(string returnUrl, ConsentInputModel model = null)
        {
            var request = await this.interaction.GetAuthorizationContextAsync(returnUrl);
            if (request != null)
            {
                var client = await this.clientStore.FindEnabledClientByIdAsync(request.ClientId);
                if (client != null)
                {
                    var resources = await this.resourceStore.FindEnabledResourcesByScopeAsync(request.ScopesRequested);
                    if (resources != null && (resources.IdentityResources.Any() || resources.ApiResources.Any()))
                    {
                        return this.CreateConsentViewModel(model, returnUrl, request, client, resources);
                    }
                    else
                    {
                        this.logger.LogError("No scopes matching: {0}", request.ScopesRequested.Aggregate((x, y) => x + ", " + y));
                    }
                }
                else
                {
                    this.logger.LogError("Invalid client id: {0}", request.ClientId);
                }
            }
            else
            {
                this.logger.LogError("No consent request matching request: {0}", returnUrl);
            }

            return null;
        }

        private ConsentViewModel CreateConsentViewModel(
            ConsentInputModel model, string returnUrl,
            AuthorizationRequest request,
            Client client, Resources resources)
        {
            var vm = new ConsentViewModel();
            vm.RememberConsent = model?.RememberConsent ?? true;
            vm.ScopesConsented = model?.ScopesConsented ?? Enumerable.Empty<string>();

            vm.ReturnUrl = returnUrl;

            vm.ClientName = client.ClientName;
            vm.ClientUrl = client.ClientUri;
            vm.ClientLogoUrl = client.LogoUri;
            vm.AllowRememberConsent = client.AllowRememberConsent;

            vm.IdentityScopes = resources.IdentityResources.Select(x => this.CreateScopeViewModel(x, vm.ScopesConsented.Contains(x.Name) || model == null)).ToArray();
            vm.ResourceScopes = resources.ApiResources.SelectMany(x => x.Scopes).Select(x => this.CreateScopeViewModel(x, vm.ScopesConsented.Contains(x.Name) || model == null)).ToArray();
            if (ConsentOptions.EnableOfflineAccess && resources.OfflineAccess)
            {
                vm.ResourceScopes = vm.ResourceScopes.Union(new ScopeViewModel[] {
                    this.GetOfflineAccessScope(vm.ScopesConsented.Contains(IdentityServerConstants.StandardScopes.OfflineAccess) || model == null)
                });
            }

            return vm;
        }

        public ScopeViewModel CreateScopeViewModel(IdentityResource identity, bool check)
        {
            return new ScopeViewModel
            {
                Name = identity.Name,
                DisplayName = identity.DisplayName,
                Description = identity.Description,
                Emphasize = identity.Emphasize,
                Required = identity.Required,
                Checked = check || identity.Required,
            };
        }

        public ScopeViewModel CreateScopeViewModel(Scope scope, bool check)
        {
            return new ScopeViewModel
            {
                Name = scope.Name,
                DisplayName = scope.DisplayName,
                Description = scope.Description,
                Emphasize = scope.Emphasize,
                Required = scope.Required,
                Checked = check || scope.Required,
            };
        }

        private ScopeViewModel GetOfflineAccessScope(bool check)
        {
            return new ScopeViewModel
            {
                Name = IdentityServerConstants.StandardScopes.OfflineAccess,
                DisplayName = ConsentOptions.OfflineAccessDisplayName,
                Description = ConsentOptions.OfflineAccessDescription,
                Emphasize = true,
                Checked = check
            };
        }
    }
}

