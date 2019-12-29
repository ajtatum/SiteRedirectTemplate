using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using BabouExtensions;
using Dapper;
using IpStack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SiteRedirectTemplate.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly IConfiguration _config;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IHttpContextAccessor accessor, IConfiguration config, ILogger<IndexModel> logger)
        {
            _accessor = accessor;
            _config = config;
            _logger = logger;
        }

        public IActionResult OnGet(string id = null)
        {
            if (id != null)
            {
                var blockPathList = _config["BlockedPaths"].Split(';').ToList();

                if (blockPathList.Contains(id))
                    return new UnauthorizedResult();

                var sqlConnectionString = _config.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(sqlConnectionString))
                {
                    try
                    {
                        var shortenedUrl = connection.QueryFirst<AJT.Dtos.ShortenedUrlDto>("SELECT * FROM ShortenedUrls WHERE Token = @Token AND Domain = @Domain", new { Token = id, Domain = _config["Domain"] });

                        if (shortenedUrl == null)
                            return new RedirectResult($"https://babou.io?Message=The token {id} no longer exists.", false);

                        #region GetReferrer
                        var referrerUrl = string.Empty;

                        if (!Request.Query["src"].ToString().IsNullOrWhiteSpace())
                        {
                            var srcReferrer = Request.Query["src"].ToString();
                            if (srcReferrer.TryGetUrl(out var url))
                            {
                                referrerUrl = url;
                            }
                        }

                        if (referrerUrl.IsNullOrWhiteSpace())
                            referrerUrl = Request.Headers["Referer"].ToString();

                        try
                        {
                            if (referrerUrl.IsNullOrEmpty())
                            {
                                var header = Request.GetTypedHeaders();
                                var uriReferer = header.Referer;

                                if (uriReferer != null)
                                    referrerUrl = uriReferer.AbsoluteUri;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Attempt to retrieve header referer.");
                        }

                        if (referrerUrl.IsNullOrEmpty())
                            referrerUrl = null;

                        referrerUrl = referrerUrl.WithMaxLength(500);
                        #endregion

                        #region GetIPAddress
                        var ipAddress = _accessor.HttpContext.Connection.RemoteIpAddress.ToString();
#if DEBUG
                        ipAddress = "44.21.199.18";
#endif
                        string city = null;
                        string state = null;
                        string country = null;
                        float? latitude = null;
                        float? longitude = null;

                        try
                        {
                            var client = new IpStackClient(_config["IpStackApiKey"], true);
                            var ipAddressDetails = client.GetIpAddressDetails(ipAddress);
                            city = ipAddressDetails.City;
                            state = ipAddressDetails.RegionCode;
                            country = ipAddressDetails.CountryCode;
                            latitude = (float) ipAddressDetails.Latitude;
                            longitude = (float) ipAddressDetails.Longitude;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error receiving IP Address Details from IP Stack.");
                        }
                        #endregion

                        var p = new DynamicParameters();
                        p.Add("@ShortenedUrlId", shortenedUrl.Id);
                        p.Add("@ClickDate", DateTime.Now);
                        p.Add("@Referrer", referrerUrl);
                        p.Add("@City", city);
                        p.Add("@State", state);
                        p.Add("@Country", country);
                        p.Add("@Latitude", latitude);
                        p.Add("@Longitude", longitude);

                        connection.Execute("InsertShortenedUrlClick", p, commandType: CommandType.StoredProcedure);

                        _logger.LogInformation("Redirected {ShortUrl} to {LongUrl}. Referred by {Referrer}", shortenedUrl.ShortUrl, shortenedUrl.LongUrl, referrerUrl.IsNullOrEmpty() ? "Unknown" : referrerUrl);

                        return new RedirectResult(shortenedUrl.LongUrl, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error with token: {Token}.", id);
                        return new RedirectResult($"https://babou.io?Message=Error: Sorry, there was an error with {id}. Sign up here to create your own short urls and more!", false);
                    }
                }
            }

            return Page();
        }
    }
}
