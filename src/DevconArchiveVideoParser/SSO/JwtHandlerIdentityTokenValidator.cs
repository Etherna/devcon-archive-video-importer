using Etherna.DevconArchiveVideoParser.Extensions;
using IdentityModel;
using IdentityModel.OidcClient;
using IdentityModel.OidcClient.Results;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoParser.SSO
{
    public class JwtHandlerIdentityTokenValidator : IIdentityTokenValidator
    {
        // Methods.
        public Task<IdentityTokenValidationResult> ValidateAsync(
            string identityToken,
            OidcClientOptions options,
            CancellationToken cancellationToken = default)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            // Setup general validation parameters.
            var parameters = new TokenValidationParameters
            {
                ValidIssuer = options.ProviderInformation.IssuerName,
                ValidAudience = options.ClientId,
                ValidateIssuer = options.Policy.ValidateTokenIssuerName,
                NameClaimType = JwtClaimTypes.Name,
                RoleClaimType = JwtClaimTypes.Role,
                ClockSkew = options.ClockSkew
            };

            // Read the token signing algorithm.
            var handler = new JsonWebTokenHandler();
            JsonWebToken jwt;

            try
            {
                jwt = handler.ReadJsonWebToken(identityToken);
            }
            catch (Exception ex)
            {
                return Task.FromResult(new IdentityTokenValidationResult
                {
                    Error = $"Error validating identity token: {ex}"
                });
            }

            var algorithm = jwt.Alg;

            // if token is unsigned, and this is allowed, skip signature validation
            if (string.Equals(algorithm, "none", StringComparison.OrdinalIgnoreCase))
            {
                if (options.Policy.RequireIdentityTokenSignature)
                    return Task.FromResult(new IdentityTokenValidationResult
                    {
                        Error = $"Identity token is not signed. Signatures are required by policy"
                    });
                else
                    parameters.RequireSignedTokens = false;
            }
            else if (!options.Policy.ValidSignatureAlgorithms.Contains(algorithm))
                // Check if signature algorithm is allowed by policy.
                return Task.FromResult(new IdentityTokenValidationResult
                {
                    Error = $"Identity token uses invalid algorithm: {algorithm}"
                });

            var result = ValidateSignature(identityToken, handler, parameters, options);
            if (result.IsValid == false)
            {
                if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
                    return Task.FromResult(new IdentityTokenValidationResult
                    {
                        Error = "invalid_signature"
                    });
                if (result.Exception is SecurityTokenUnableToValidateException)
                    return Task.FromResult(new IdentityTokenValidationResult
                    {
                        Error = "unable_to_validate_token"
                    });

                throw result.Exception;
            }

            var user = new ClaimsPrincipal(result.ClaimsIdentity);

            var error = CheckRequiredClaim(user);
            if (error is not null && error.IsPresent())
                return Task.FromResult(new IdentityTokenValidationResult
                {
                    Error = error
                });

            return Task.FromResult(new IdentityTokenValidationResult
            {
                User = user,
                SignatureAlgorithm = algorithm
            });
        }

        private TokenValidationResult ValidateSignature(
            string identityToken,
            JsonWebTokenHandler handler,
            TokenValidationParameters parameters,
            OidcClientOptions options)
        {
            if (parameters.RequireSignedTokens)
            {
                // Read keys from provider information.
                var keys = new List<SecurityKey>();

                foreach (var webKey in options.ProviderInformation.KeySet.Keys)
                {
                    if (webKey.E.IsPresent() && webKey.N.IsPresent())
                    {
                        // only add keys used for signatures
                        if (webKey.Use == "sig" || webKey.Use == null)
                        {
                            var e = Base64Url.Decode(webKey.E);
                            var n = Base64Url.Decode(webKey.N);

                            var key = new RsaSecurityKey(new RSAParameters { Exponent = e, Modulus = n })
                            {
                                KeyId = webKey.Kid
                            };

                            keys.Add(key);
                        }
                    }
                    else if (webKey.X.IsPresent() && webKey.Y.IsPresent() && webKey.Crv.IsPresent())
                    {
                        using var ec = ECDsa.Create(new ECParameters
                        {
                            Curve = GetCurveFromCrvValue(webKey.Crv),
                            Q = new ECPoint
                            {
                                X = Base64Url.Decode(webKey.X),
                                Y = Base64Url.Decode(webKey.Y)
                            }
                        });

                        var key = new ECDsaSecurityKey(ec)
                        {
                            KeyId = webKey.Kid
                        };

                        keys.Add(key);
                    }
                }

                parameters.IssuerSigningKeys = keys;
            }

            return handler.ValidateToken(identityToken, parameters);
        }

        // Helpers.
        private static string? CheckRequiredClaim(ClaimsPrincipal user)
        {
            var requiredClaims = new List<string>
            {
                JwtClaimTypes.Issuer,
                JwtClaimTypes.Subject,
                JwtClaimTypes.IssuedAt,
                JwtClaimTypes.Audience,
                JwtClaimTypes.Expiration,
            };

            foreach (var claimType in requiredClaims)
            {
                var claim = user.FindFirst(claimType);
                if (claim == null)
                {
                    return $"{claimType} claim is missing";
                }
            }

            return null;
        }

        private static ECCurve GetCurveFromCrvValue(string crv)
        {
            return crv switch
            {
                JsonWebKeyECTypes.P256 => ECCurve.NamedCurves.nistP256,
                JsonWebKeyECTypes.P384 => ECCurve.NamedCurves.nistP384,
                JsonWebKeyECTypes.P521 => ECCurve.NamedCurves.nistP521,
                _ => throw new InvalidOperationException($"Unsupported curve type of {crv}"),
            };
        }
    }
}
