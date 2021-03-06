﻿//-----------------------------------------------------------------------
// <copyright file="SharedKeyValidatingHandler.cs" company="Microsoft">
//    Copyright 2012 Microsoft Corporation
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Idunno.WebApi.SharedKeyAuthentication
{
    /// <summary>
    /// Validates an inbound message which uses shared key authentication.
    /// </summary>
    public class SharedKeyValidatingHandler : DelegatingHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SharedKeyValidatingHandler" /> class.
        /// </summary>
        /// <param name="sharedSecretResolver">A function to resolve an account name to a shared secret.</param>
        public SharedKeyValidatingHandler(Func<string, byte[]> sharedSecretResolver)
        {
            this.SharedSecretResolver = sharedSecretResolver;
            this.MaximumMessageAge = new TimeSpan(0, 0, 5, 0);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedKeyValidatingHandler" /> class.
        /// </summary>
        /// <param name="sharedSecretResolver">A function to resolve an account name to a shared secret.</param>
        /// <param name="maximumMessageAge">The maximum time period a message is considered valid for.</param>
        public SharedKeyValidatingHandler(Func<string, byte[]> sharedSecretResolver, TimeSpan maximumMessageAge)
            : this(sharedSecretResolver)
        {
            this.MaximumMessageAge = maximumMessageAge;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedKeyValidatingHandler" /> class.
        /// </summary>
        /// <param name="sharedSecretResolver">A function to resolve an account name to a shared secret.</param>
        /// <param name="maximumMessageAge">The maximum time period a message is considered valid for.</param>
        /// <param name="claimsAuthenticationManager">The claims authentication manager to use to transform claims.</param>
        public SharedKeyValidatingHandler(Func<string, byte[]> sharedSecretResolver, TimeSpan maximumMessageAge, ClaimsAuthenticationManager claimsAuthenticationManager)
            : this(sharedSecretResolver, maximumMessageAge)
        {
            this.ClaimsAuthenticationManager = claimsAuthenticationManager;
        }

        /// <summary>
        /// Gets or sets the shared secret resolver.
        /// </summary>
        /// <value>
        /// The shared secret resolver.
        /// </value>
        protected Func<string, byte[]> SharedSecretResolver
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the age after which a message will be rejected.
        /// </summary>
        /// <value>
        /// The maximum message age.
        /// </value>
        protected TimeSpan MaximumMessageAge
        {
            get; 
            set;
        }

        /// <summary>
        /// Gets or sets the claims authentication manager to use to transform claims.
        /// </summary>
        /// <value>
        /// The claims authentication manager.
        /// </value>
        protected ClaimsAuthenticationManager ClaimsAuthenticationManager
        {
            get;
            set;
        }

        /// <summary>
        /// Sends an HTTP request to the inner handler to send to the server as an asynchronous operation.
        /// </summary>
        /// <param name="request">The HTTP request message to send to the server.</param>
        /// <param name="cancellationToken">A cancellation token to cancel operation.</param>
        /// <returns>
        /// Returns <see cref="T:System.Threading.Tasks.Task`1" />. The task object representing the asynchronous operation.
        /// </returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            try
            {
                var principal = SignatureValidator.Validate(request, this.SharedSecretResolver, this.MaximumMessageAge);
                
                if (this.ClaimsAuthenticationManager != null)
                {
                    principal = this.ClaimsAuthenticationManager.Authenticate(request.RequestUri.ToString(), principal);
                }
                
                this.SetPrincipal(principal);
            }
            catch (UnauthorizedException)
            {
                return Unauthorized();
            }
            catch (ForbiddenException fbex)
            {
                return Forbidden(fbex.Message);
            }
            catch (PreconditionFailedException pcex)
            {
                return PreconditionFailed(pcex.Message);
            }
            
            return base.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Attaches the specified principal to the current thread and HTTP Context if one exists.
        /// </summary>
        /// <param name="principal">The principal to attach.</param>
        protected virtual void SetPrincipal(ClaimsPrincipal principal)
        {
            Thread.CurrentPrincipal = principal;
            if (HttpContext.Current != null)
            {
                HttpContext.Current.User = principal;
            }
        }

        /// <summary>
        /// Returns an unauthorized response.
        /// </summary>
        /// <returns>An unauthorized response.</returns>
        private static Task<HttpResponseMessage> Unauthorized()
        {
            return Task<HttpResponseMessage>.Factory.StartNew(() => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }

        /// <summary>
        /// Returns a forbidden response, with the specified reason.
        /// </summary>
        /// <param name="reason">The reason the request is unauthorized.</param>
        /// <returns>A forbidden response.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "WebApi will clean this up further up the chain.")]
        private static Task<HttpResponseMessage> Forbidden(string reason)
        {
            return Task<HttpResponseMessage>.Factory.StartNew(() => new HttpResponseMessage(HttpStatusCode.Forbidden) { ReasonPhrase = reason });
        }

        /// <summary>
        /// Returns a precondition failed response, with the specified reason.
        /// </summary>
        /// <param name="reason">The reason the request is unauthorized.</param>
        /// <returns>A precondition failed response.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "WebApi will clean this up further up the chain.")]
        private static Task<HttpResponseMessage> PreconditionFailed(string reason)
        {
            return Task<HttpResponseMessage>.Factory.StartNew(() => new HttpResponseMessage(HttpStatusCode.PreconditionFailed) { ReasonPhrase = reason });
        }
    }
}
