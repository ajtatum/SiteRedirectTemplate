using System;
using Babou.AspNetCore.SecurityExtensions.ContentSecurityPolicy;
using Babou.AspNetCore.SecurityExtensions.ExpectCT;
using Babou.AspNetCore.SecurityExtensions.FeaturePolicy;
using Babou.AspNetCore.SecurityExtensions.FrameOptions;
using Babou.AspNetCore.SecurityExtensions.ReferrerPolicy;
using Babou.AspNetCore.SecurityExtensions.XContentTypeOptions;
using Babou.AspNetCore.SecurityExtensions.XRobotsTag;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SiteRedirectTemplate
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();

            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            // forwarded Header middleware
            var fordwardedHeaderOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };
            fordwardedHeaderOptions.KnownNetworks.Clear();
            fordwardedHeaderOptions.KnownProxies.Clear();

            app.UseForwardedHeaders(fordwardedHeaderOptions);

            app.Use(async (context, next) =>
            {
                // Request method, scheme, and path
                logger.LogInformation("Request Method: {Method}", context.Request.Method);
                logger.LogInformation("Request Scheme: {Scheme}", context.Request.Scheme);
                logger.LogInformation("Request Path: {Path}", context.Request.Path);

                // Headers
                foreach (var header in context.Request.Headers)
                {
                    logger.LogInformation("Header: {Key}: {Value}", header.Key, header.Value);
                }

                // Connection: RemoteIp
                logger.LogInformation("Request RemoteIp: {RemoteIpAddress}", context.Connection.RemoteIpAddress);

                await next();
            });

            app.UseContentSecurityPolicy(new CspDirectiveList
            {
                DefaultSrc = CspDirective.Self.AddHttpsScheme(),
                StyleSrc = StyleCspDirective.Self.AddUnsafeInline().AddHttpsScheme(),
                ScriptSrc = ScriptCspDirective.Self.AddUnsafeInline().AddHttpsScheme(),
                ImgSrc = CspDirective.Self.AddDataScheme().AddHttpsScheme(),
                FontSrc = CspDirective.Self.AddHttpsScheme(),
                ConnectSrc = CspDirective.Self.AddHttpsScheme()
            });

            app.UseFeaturePolicy(
                new FeatureDirectiveList()
                    .AddNone(PolicyFeature.Microphone)
                    .AddNone(PolicyFeature.Camera)
                    .AddSelf(PolicyFeature.FullScreen)
            );

            app.UseFrameOptions(FrameOptionsPolicy.Deny);

            app.UseXContentTypeOptions(XContentTypeOptions.NoSniff);

            app.UseReferrerPolicy(ReferrerPolicy.UnsafeUrl);

            app.UseXRobotsTag(false, false);

            app.UseExpectCT(true, new TimeSpan(7,0,0,0), new Uri("https://ajtio.report-uri.com/r/d/ct/enforce"));

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
            });
        }
    }
}
