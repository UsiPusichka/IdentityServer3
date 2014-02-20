﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Protocols.WSTrust;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Security.Claims;
using Thinktecture.IdentityServer.Core.Connect.Models;
using Thinktecture.IdentityServer.Core.Plumbing;
using Thinktecture.IdentityServer.Core.Services;

namespace Thinktecture.IdentityServer.Core.Connect.Services
{
    public class DefaultTokenService : ITokenService
    {
        private IUserService _profile;
        private ICoreSettings _settings;
        private IClaimsProvider _claimsProvider;

        public DefaultTokenService(IUserService profile, ICoreSettings settings, IClaimsProvider claimsProvider)
        {
            _profile = profile;
            _settings = settings;
            _claimsProvider = claimsProvider;
        }

        public virtual Token CreateIdentityToken(ValidatedAuthorizeRequest request, ClaimsPrincipal user)
        {
            // minimal, mandatory claims
            var claims = new List<Claim>
            {
                new Claim(Constants.ClaimTypes.Subject, user.GetSubject()),
                new Claim(Constants.ClaimTypes.AuthenticationMethod, user.GetAuthenticationMethod()),
                new Claim(Constants.ClaimTypes.AuthenticationTime, user.GetAuthenticationTimeEpoch().ToString())
            };

            // if nonce was sent, must be mirrored in id token
            if (request.Nonce.IsPresent())
            {
                claims.Add(new Claim(Constants.ClaimTypes.Nonce, request.Nonce));
            }

            claims.AddRange(_claimsProvider.GetIdentityTokenClaims(
                user,
                request.Client,
                request.Scopes,
                _settings,
                !request.AccessTokenRequested,
                _profile));

            var token = new Token(Constants.TokenTypes.IdentityToken)
            {
                Audience = request.ClientId,
                Issuer = _settings.GetIssuerUri(),
                Lifetime = request.Client.IdentityTokenLifetime,
                Claims = claims.Distinct(new ClaimComparer()).ToList()
            };

            return token;
        }

        public virtual Token CreateAccessToken(ValidatedAuthorizeRequest request, ClaimsPrincipal user)
        {
            // minimal claims
            var claims = new List<Claim>
            {
                new Claim(Constants.ClaimTypes.Subject, user.GetSubject()),
                new Claim(Constants.ClaimTypes.ClientId, request.ClientId),
                new Claim(Constants.ClaimTypes.Scope, request.Scopes.ToSpaceSeparatedString())
            };

            claims.AddRange(_claimsProvider.GetAccessTokenClaims(
                user, 
                request.Client, 
                request.Scopes, 
                _settings, 
                _profile));

            var token = new Token(Constants.TokenTypes.AccessToken)
            {
                Audience = _settings.GetIssuerUri() + "/resources",
                Issuer = _settings.GetIssuerUri(),
                Lifetime = request.Client.AccessTokenLifetime,
                Claims = claims
            };

            return token;
        }

        public virtual string CreateJsonWebToken(Token token, SigningCredentials credentials)
        {
            var jwt = new JwtSecurityToken(
                token.Issuer,
                token.Audience,
                token.Claims,
                new Lifetime(DateTime.UtcNow, DateTime.UtcNow.AddSeconds(token.Lifetime)),
                credentials);

            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(jwt);
        }

        public virtual Token CreateIdentityToken(ValidatedTokenRequest request, ClaimsPrincipal user)
        {
            return request.AuthorizationCode.IdentityToken;
        }

        public virtual Token CreateAccessToken(ValidatedTokenRequest request, ClaimsPrincipal user)
        {
            return request.AuthorizationCode.AccessToken;
        }
    }
}
