using System;
using System.Data.SqlClient;
using BabouExtensions;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SiteRedirectTemplate.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IConfiguration config, ILogger<IndexModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        public IActionResult OnGet(string id = null)
        {
            if (id != null)
            {
                var sqlConnectionString = _config.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(sqlConnectionString))
                {
                    try
                    {
                        var shortenedUrl = connection.QueryFirst<AJT.Dtos.ShortenedUrlDto>("SELECT * FROM ShortenedUrls WHERE Token = @Token AND Domain = @Domain", new { Token = id, Domain = _config["Domain"] });

                        if (shortenedUrl == null)
                            return new RedirectResult($"https://api.ajt.io?Message=The token {id} no longer exists.", false);

                        var referer = Request.Headers["Referer"].ToString();

                        try
                        {
                            if (referer.IsNullOrEmpty())
                            {
                                var header = Request.GetTypedHeaders();
                                var uriReferer = header.Referer;

                                if (uriReferer != null) 
                                    referer = uriReferer.AbsoluteUri;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Attempt to retrieve header referer.");
                        }

                        if (referer.IsNullOrEmpty())
                            referer = null;

                        referer = referer.WithMaxLength(500);

                        var click = new AJT.Dtos.ShortenedUrlClickDto()
                        {
                            ShortenedUrlId = shortenedUrl.Id,
                            ClickDate = DateTime.Now,
                            Referrer = referer
                        };

                        connection.Execute("INSERT INTO ShortenedUrlClicks (ShortenedUrlId, ClickDate, Referrer) VALUES (@ShortenedUrlId, @ClickDate, @Referrer)", new { click.ShortenedUrlId, click.ClickDate, click.Referrer });

                        _logger.LogInformation("Redirected {ShortUrl} to {LongUrl}. Referred by {Referrer}", shortenedUrl.ShortUrl, shortenedUrl.LongUrl, referer.IsNullOrEmpty() ? "Unknown" : referer);

                        return new RedirectResult(shortenedUrl.LongUrl, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error with token: {Token}.", id);
                        return new RedirectResult($"https://api.ajt.io?Message=Error: Sorry, there was an error with {id}. Sign up here to create your own short urls and more!", false);
                    }
                }
            }

            return Page();
        }
    }
}
