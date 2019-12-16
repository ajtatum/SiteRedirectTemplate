using System;
using System.Data.SqlClient;
using BabouExtensions;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;

namespace SiteRedirectTemplate.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _config;

        public IndexModel(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult OnGet(string id = null)
        {
            if (id != null)
            {
                var sqlConnectionString = _config.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(sqlConnectionString))
                {
                    var shortenedUrl = connection.QueryFirst<AJT.Dtos.ShortenedUrlDto>("SELECT * FROM ShortenedUrls WHERE Token = @Token AND Domain = '@Domain'", new {Token = id, Domain = _config["Domain"]});

                    if (shortenedUrl == null)
                        return new RedirectResult($"https://api.ajt.io?Message=The token {id} no longer exists.", false);

                    var click = new AJT.Dtos.ShortenedUrlClickDto()
                    {
                        ShortenedUrlId = shortenedUrl.Id,
                        ClickDate = DateTime.Now,
                        Referrer = HttpContext.Request.Headers[HeaderNames.Referer].ToString().Truncate(500, false)
                    };

                    connection.Execute("INSERT INTO ShortenedUrlClicks (ShortenedUrlId, ClickDate, Referrer) VALUES (@ShortenedUrlId, @ClickDate, @Referrer)", new {click.ShortenedUrlId, click.ClickDate, click.Referrer});

                    return new RedirectResult(shortenedUrl.LongUrl, false);
                }
            }
            else
            {
                return new RedirectResult($"https://api.ajt.io?Message=Create your own short short url by signing up here.", false);
            }
        }
    }
}
