using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.Entities.Enums;
using Kavita.Server.Attributes;
using Kavita.Services.Helpers.Kobo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

[Route("api/v3/content")]
[KoboDeviceAuthenticate]
[Authorize(PolicyGroups.DownloadPolicy)]
[ServerSettingEnabled(ServerSettingKey.EnableKobo, "kobo-sync-disabled")]
public class KoboAnnotationController(
    ILogger<KoboAnnotationController> logger,
    IUnitOfWork unitOfWork,
    IKoboSyncService koboSyncService,
    IHttpClientFactory httpClientFactory,
    IDirectoryService directoryService,
    IDownloadService downloadService): BaseApiController
{

    private HttpClient KoboReadingServicesClient => httpClientFactory.CreateClient("KoboReadingServices");
    private JsonSerializerOptions JsonSerializerOptions => KoboJsonOptions.JsonSerializerOptions;

    [HttpGet("{bookId}/annotations")]
    public async Task<ActionResult> GetAnnotations(string bookId, [FromQuery] int limit = 100)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await koboSyncService.ProxyRequestIfEnabled();
        }

        // TODO: Limit by query
        var annotations = await koboSyncService.GetKoboAnnotationsAsync(UserId, [kavitaId], ct);

        return Ok(annotations);
    }

    [HttpPost("checkforchanges")]
    public async Task<ActionResult> GetAnnotationsForBook()
    {
        var ct = HttpContext.RequestAborted;

        var array = await JsonSerializer.DeserializeAsync<List<JsonNode>>(Request.Body, options: JsonSerializerOptions, cancellationToken: ct);
        if (array is null)
        {
            return BadRequest();
        }

        var isKoboRequest = array.Any(book => !int.TryParse(book["ContentId"]?.GetValue<string>(), out _));
        if (isKoboRequest)
        {
            return await koboSyncService.ProxyRequestIfEnabled(KoboReadingServicesClient, array);
        }

        var bookIds = array.Select(book => book["ContentId"]?.GetValue<string>())
            .Distinct()
            .WhereNotNull()
            .Select(int.Parse)
            .ToList();

        // It seems that this request is only made when opening a book (I.e. bookIds.Count == 1). For this reason we won't bother
        // With the ETag nonsense
        Response.Headers.ETag = "Kavita: I will ignore this";

        var annotations = await koboSyncService.GetKoboAnnotationsAsync(UserId, bookIds, ct);

        return Ok(new
        {
            Annotations = annotations,
        });
    }

    [HttpPatch("{bookId}/annotations")]
    public async Task<ActionResult> UpdateAnnotations(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await koboSyncService.ProxyRequestIfEnabled(KoboReadingServicesClient);
        }

        var koboAnnotations = await JsonSerializer.DeserializeAsync<KoboPatchAnnotationsDto>(Request.Body, options: JsonSerializerOptions , cancellationToken: ct);
        if (koboAnnotations is null)
        {
            return BadRequest();
        }

        await koboSyncService.UpdateKoboAnnotationsAsync(UserId, kavitaId, koboAnnotations, ct);

        return Accepted();
    }

}
