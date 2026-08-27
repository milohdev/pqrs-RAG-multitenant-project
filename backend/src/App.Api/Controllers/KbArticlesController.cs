using App.Application.KbArticles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/kb-articles")]
public class KbArticlesController : ControllerBase
{
    private readonly KbArticleService _service;
    public KbArticlesController(KbArticleService service) => _service = service;

    [HttpGet]
    public Task<List<Domain.Entities.KnowledgeBaseArticle>> List(CancellationToken ct) => _service.ListAsync(ct);

    [HttpPost]
    public async Task<IActionResult> Create(UpsertKbArticleDto dto, CancellationToken ct)
        => Ok(await _service.CreateAsync(dto, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpsertKbArticleDto dto, CancellationToken ct)
    {
        var article = await _service.UpdateAsync(id, dto, ct);
        return article is null ? NotFound() : Ok(article);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}